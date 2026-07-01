# Cafe Management System — Architecture & Module Guide

A multi-tenant SaaS restaurant/cafe platform built on **ASP.NET Core 8 MVC**, **EF Core 9**, **SQL Server**, Razor views, SignalR, and a design-system UI. This document explains what every module does, the **business logic** behind it, and **how the modules connect**.

> TL;DR — Yes, the modules carry real business logic: atomic stock deduction, double‑entry accounting, idempotent money operations, per‑tenant isolation enforced at the database query level, and provider abstractions for billing/tax/SMS. Nothing here is a CRUD‑only shell; the tables are wired to services that enforce rules.

---

## 1. Technology & layering

| Layer | What lives here |
|---|---|
| **Controllers/** | HTTP endpoints. Thin — they authorize, bind input, call a service, return a View/JSON. |
| **Services/** | Business logic. All money/stock/accounting rules live here, not in controllers. |
| **Models/** | EF entities (each tenant-owned entity implements `ITenantOwned`) + view models + request DTOs. |
| **Data/ApplicationDbContext.cs** | The EF model: DbSets, per-phase `ConfigurePhaseN…` methods, global query filters, check constraints, unique indexes, interceptors. |
| **Views/** | Razor + a shared design system (`kpi-card`, `premium-table`, `btn-*`, `ActionCard` toasts). AJAX posts carry the antiforgery token in a `RequestVerificationToken` header. |
| **Attributes/** | RBAC + feature-gate filters (`[RequireOwner]`, `[RequireFeature("X")]`, …). |
| **Migrations/** | One additive EF migration per phase. |

**Cross-cutting infrastructure**
- **Interceptors** — `TenantStampingInterceptor` (stamps `TenantId` on insert) runs *before* the audit interceptor (writes `AuditLog`).
- **Middleware** — `TenantResolutionMiddleware` resolves the active tenant (session → subdomain → `X-Tenant` header) *before* the auth gate.
- **Background workers** — `EmailBackgroundWorker` and `SmsBackgroundWorker` drain their queues every 60s (`IgnoreQueryFilters` because a background scope has no tenant).
- **Localization** — English (default) + Urdu (RTL), culture via cookie; shared string catalog `Resources/SharedResource.ur.resx`.

---

## 2. Multi-tenancy foundation (Phase 0) — the spine everything hangs off

Every business's data lives in one shared database, isolated logically:

- **`ITenantOwned { int TenantId }`** — implemented by every tenant-scoped entity. A convention loop in `ApplicationDbContext.ConfigureMultiTenancy` auto-applies to each such entity: a **global query filter** (`IgnoreTenantFilterFlag || e.TenantId == CurrentTenantIdValue`), the FK to `Tenant`, and an index on `TenantId`.
- **`ITenantContext`** (scoped) holds the current `TenantId`; the query filter reads it live. `TenantStampingInterceptor` stamps it on every insert, so application code never sets `TenantId` by hand.
- **Platform vs tenant** — `User` and `AuditLog` carry a *nullable* `TenantId`; a `PlatformAdmin` is tenant-less and manages tenants/plans from the `/Platform` console. Impersonation swaps the session into a tenant (fully audited).
- **Feature gating** — `[RequireFeature("X")]` + `IFeatureGate` read the tenant's plan (Free/Starter/Pro); the sidebar hides locked modules. `FeatureCatalog.Core` is always on.

**Non-obvious rule that governs every module:** natural-key unique indexes must be **composite with `TenantId`** — e.g. `(TenantId, Category.Name)`, `(TenantId, Order.OrderNumber)`. A globally-unique index would stop a second tenant from reusing a name/number and break provisioning. Only `User.Email`, `Tenant.Slug`, `Plan.Name` are globally unique.

> **Connection:** Phase 0 is a dependency of *all* later phases. Any new entity implements `ITenantOwned` and inherits isolation for free.

---

## 3. Identity, RBAC & audit

- **Auth** — session-based; `BCrypt` password hashes. Logins seeded for demo: platform `platform@cafe.com / platform123`, Demo owner `admin@cafe.com / admin123`, customers `alice@example.com / cust123`.
- **Roles** (ascending) — `Customer` → `Staff` → `BranchManager` → `Owner` (tenant admin) → `PlatformAdmin`. Enforced by attributes: `[RequireStaffOrAbove]`, `[RequireManagerOrOwner]`, `[RequireOwner]`, `[RequirePlatformAdmin]`, `[RequireCustomer]`.
- **Branch scoping** — a `BranchManager` only sees their branch; `BaseController.CanAccessBranch()` / `GetAccessibleBranches()` enforce it on every branch-scoped read/write.
- **Audit** — the audit interceptor records create/update/delete with actor + branch into `AuditLog`; controllers also call `IAuditService.LogAsync` for domain actions (approvals, auto-post, impersonation).

---

## 4. The modules

Each entry lists **purpose → key entities → business logic → connections**.

### 4.1 POS & Restaurant Core (Phase 1)
- **Purpose:** the touch register — cart, tiled menu, split payments, hold/draft/resume, table & kitchen flow.
- **Entities:** `RestaurantTable`, `Order` (+ service type, kitchen status, hold state, nullable `CustomerId`), `OrderItem` (+ line discount, notes), `Payment` (split tenders), `Invoice`.
- **Business logic (`IPosService.FinalizeAsync`):**
  1. Idempotency guard via a client ref (double-submit protection).
  2. Build/validate the order **server-side** — prices, modifier deltas, combo expansion and line discounts are recomputed on the server; the client cannot tamper with money.
  3. **Atomic stock deduction** (`IInventoryService.DeductInventoryForOrder`) inside a transaction — a brand-new order is rolled back if stock is insufficient.
  4. Create the `Invoice` (promo/partnership/packing/shipping/tax), record split `Payment`s, derive `PaymentStatus` (with a hardware-terminal path that waits for a webhook).
  5. On paid + customer → **earn loyalty** (idempotent per invoice); mark table occupied; push the ticket to the KDS over SignalR.
- **Connections:** consumes **Menu** (items/modifiers/combos/price groups), **Inventory** (recipes), **Checkout pricing** (promo/partnership/tax), **Loyalty/Gift cards** (as tenders), **Kitchen** (SignalR), **Tables**, **Accounting** (invoices are later auto-posted).

### 4.2 Menu & Product Depth (Phase 2)
- **Purpose:** rich catalog — brands, units, modifiers, combos, tiered pricing, time/day availability, SKU scan, serial/warranty.
- **Entities:** `Brand`, `Unit` (+ base/conversion), `ModifierGroup`/`Modifier`/`MenuItemModifierGroup`, `Combo`/`ComboItem`, `PriceGroup`/`MenuItemPrice`; `MenuItem` gains availability window, day-mask, SKU, serial/warranty.
- **Business logic:** `MenuAvailability` filters the register to items available *right now* (time window + day mask). Modifier **price deltas** and **combo expansion** (components minus combo price = discount) are computed server-side. Price groups override the base price per item.
- **Connections:** feeds **POS** (the register calls `GetMenu/GetModifiers/GetCombos/ExpandCombo`), and **Inventory** (recipes map items → stock).

### 4.3 Inventory & Supply Chain (Phase 3)
- **Purpose:** multi-branch stock movement with an auditable ledger.
- **Entities:** `StockTransfer`/`Item`, `StockAdjustment`/`Line`, `ProductionOrder`/`Input`, plus the existing `InventoryItem`, `InventoryTransaction`, recipe mappings.
- **Business logic (`ISupplyChainService`):** every move is a **conditional atomic SQL update** (`UPDATE … WHERE Quantity >= @x`) inside one transaction, and writes an `InventoryTransaction` audit row:
  - **Transfer:** deduct source, find/create the destination item by name+unit, add.
  - **Adjustment (approve):** apply signed deltas; **reject** if a decrease would go negative.
  - **Production (complete):** consume inputs, produce output, and **roll input cost** into the output's unit cost (`totalInputCost / outputQty`).
- **Connections:** POS/returns deduct/restock through the same atomic paths; production/transfers change the cost basis used by **Accounting** and reports.

### 4.4 Sales Lifecycle, Returns & Receivables (Phase 4)
- **Purpose:** quotations, returns (both directions), AR/AP, credit sales, tax groups.
- **Entities:** `Quotation`/`Item`, `SellReturn`/`Line`, `PurchaseReturn`/`Line`, `SupplierPayment`, `TaxGroup`/`Tax`.
- **Business logic (`IReceivablesService`):**
  - **AR** = Σ invoice totals − payments − approved sell-returns; **AP** = Σ purchase costs − supplier payments − approved purchase-returns (cached ~30s, invalidated on payment).
  - Receiving a customer payment **allocates to the oldest invoices first**.
  - Returns restock/destock through the **atomic** supply-chain paths; a quotation **converts** to an order + a Pending invoice.
  - `CheckoutPricingService` applies a `TaxGroup` (possibly **compound** — a tax computed on a running base) when supplied.
- **Connections:** shares invoices with **POS/Accounting**; returns touch **Inventory**; supplier payments feed **AP** and **Accounting**.

### 4.5 Accounting (Phase 5) — double-entry, the financial backbone
- **Purpose:** a real ledger, not a summary table.
- **Entities:** `Account` (hierarchical chart of accounts), `JournalEntry`/`JournalLine`, `PaymentAccount`, `Budget`/`BudgetLine`.
- **Business logic (`IAccountingService`):**
  - `PostJournalAsync` **enforces Σdebit = Σcredit** (≥2 lines) — an unbalanced entry throws.
  - `AutoPostAsync` scans unposted source documents and posts balanced journals, **idempotent** via a unique `(TenantId, SourceType, SourceId)` filtered index:
    - Invoice → Dr Cash/AR, Cr Sales (+ Cr Tax Payable)
    - Expense → Dr OpEx, Cr Cash
    - Purchase → Dr Inventory, Cr AP · SupplierPayment → Dr AP, Cr Cash
    - SellReturn → Dr Sales, Cr AR/Cash · PurchaseReturn → Dr AP, Cr Inventory
  - Trial Balance / P&L / Balance Sheet are LINQ aggregates over `JournalLine`s; current-period profit rolls into equity.
  - Default 11-account chart is seeded per tenant (1000 Cash … 6100 Payroll).
- **Connections:** downstream of **POS, Expenses, Purchases, Returns, Supplier payments**. `ITaxInvoiceProvider` (Null default / PakFbr stub) mirrors the billing provider pattern for future FBR e-invoicing.

### 4.6 Customer Portal & Marketing (Phase 6)
- **Purpose:** online ordering, loyalty, gift cards, CRM, templates, QR menu.
- **Entities:** `LoyaltyTransaction`, `GiftCard`/`Transaction`, `NotificationTemplate`, `Lead`/`FollowUp`/`Campaign`, `SmsQueue`.
- **Business logic:**
  - **Loyalty** (`ILoyaltyService`) — signed ledger + mirrored `Customer.LoyaltyPoints` always move together; earn on paid invoice (1 pt / Rs.100, idempotent), redeem at checkout.
  - **Gift cards** (`IGiftCardService`) — issue; **atomic redeem** in a transaction; used as a POS tender.
  - **Customer** `ShopController` (`[RequireCustomer]`) — browse, place order (into the normal Order pipeline as Delivery + Pending invoice), track status.
  - **Public** `CatalogueController` — `/Catalogue/Menu/{branchId}`, no auth, a QR menu (culture-aware).
  - **CRM** — leads → follow-ups; campaigns enqueue email/SMS.
- **Connections:** loyalty/gift cards plug into **POS** as tenders; online orders reuse the **Order/Invoice** pipeline; campaigns feed the **SMS/email queues**.

### 4.7 HR Depth (Phase 7)
- **Purpose:** org chart + leave + targets on top of the existing attendance/salary engine.
- **Entities:** `Department`, `Designation`, `LeaveType`, `LeaveRequest`, `Holiday`, `SalesTarget`, `EmployeeDocument`; `Staff` gains `DepartmentId`/`DesignationId`.
- **Business logic:** approving a `LeaveRequest` **stamps `Attendance` rows** for the date range using the leave type's attendance status, so the **existing salary engine** pays them correctly. Saving a `Holiday` stamps "Holiday" attendance for active staff (idempotent). `SalesTarget` drives commission (target vs actual).
- **Connections:** writes into **Attendance**, which the **Salary** engine reads; documents use the shared **file upload**.

### 4.8 Productivity / Essentials (Phase 8)
- **Purpose:** internal workspace — documents, memos, reminders, knowledge base, direct messages.
- **Entities:** `Document`, `Memo`, `Reminder`, `Message`, `KnowledgeBaseArticle`.
- **Business logic:** creating a reminder fires a notification; sending a message pings the recipient via `INotificationService`/SignalR. Documents/employee records use the shared **`IFileStorageService`** (binary upload to `wwwroot/uploads`, size cap + extension allow-list).
- **Connections:** hooks into **Notifications/SignalR** and the shared upload service.

### 4.9 Platform & System (Phase 9) + Polish (Phase 10)
- **Logistics:** `Rider`, `Delivery` (assign/status), `Shipment` (order or stock-transfer), `PosProfile` (receipt settings). Delivery orders can be assigned to riders and tracked; shipments track carrier/tracking/status.
- **Receipts:** `PdfInvoiceService` renders an A5 invoice and an **80mm thermal** receipt (`/Invoice/Thermal/{id}`).
- **Exports:** `IExportService` (CSV + ClosedXML Excel), e.g. Order export.
- **i18n:** request localization + culture cookie; Urdu translation slice for POS + customer-facing screens; RTL.
- **Polish (Phase 10):** gift-card & loyalty **tenders** in POS; the **SMS worker**; expanded **accounting auto-post**; the HR org-chart form + holiday attendance; binary **file upload**; and the **demo data seeder** (below).

---

## 5. How the modules connect — an end-to-end sale

```
Customer/cashier picks items (Menu: availability, modifiers, combos, price group)
        │
        ▼
POS FinalizeAsync ── validates prices server-side
        ├── Inventory: atomic stock deduction (rollback if short)   ← recipes map item→stock
        ├── CheckoutPricing: promo + bank partnership + packing/shipping + tax(group)
        ├── Invoice created; Payments recorded (Cash/Card/GiftCard/Loyalty tenders)
        │        └── GiftCard/Loyalty redeemed against live balances (atomic)
        ├── Loyalty earned (idempotent) if paid + customer
        ├── Kitchen ticket pushed over SignalR (KDS)
        └── Table marked occupied
        ▼
Accounting.AutoPost (batch) → balanced JournalEntry (Dr Cash/AR, Cr Sales, Cr Tax) — idempotent
        ▼
Receivables (if credit sale) → AR increases; later payment allocates oldest-first
        ▼
Reports / Trial Balance / P&L / Balance Sheet read the journal & orders
```

Everything above is **tenant-scoped automatically** (Phase 0 filter) and **audited** (interceptor). Delivery orders additionally flow into **Logistics** (rider assignment, status), and receipts can print as A5 or 80mm thermal.

---

## 6. Correctness patterns used throughout

- **Atomic money/stock** — conditional SQL updates inside transactions; no read-modify-write races on quantity or balance.
- **Idempotency** — client refs on sales; unique `(TenantId, SourceType, SourceId)` on auto-posted journals; one loyalty earn per invoice; "seed only if empty" in the demo seeder.
- **Server-authoritative pricing** — the register never trusts client totals; the server recomputes from the menu, modifiers, combos, promos and tax.
- **Double-entry integrity** — journals must balance or they don't post.
- **Provider abstractions** — `IBillingProvider` (Manual/Stripe), `ITaxInvoiceProvider` (Null/PakFbr), `ISmsProvider` (logging stub) — swap a real gateway without touching callers.
- **Additive migrations** — one per phase, never destructive; new non-null columns get sensible defaults behind check constraints.

---

## 7. Demo data seeder

To populate every module for a walkthrough, sign in as the **Owner** and open **`/Demo/Index`** → **"Seed demo data now"** (or `POST /Demo/Seed`). It is **idempotent** — each module block only fills a table that is currently empty, so re-running is safe. It seeds menu depth, tables, supply chain, sales & returns, marketing, HR, essentials and logistics, and calls `Accounting.AutoPost` so the ledger reflects real invoices/expenses/purchases. Implemented in `Services/DemoDataService.cs`, exposed by `DemoController` (`[RequireOwner]`).

---

## 8. Running locally

- **Run:** `dotnet run --launch-profile http` → `http://localhost:5096`.
- **DB:** SQL Server `BILAL\SQLEXPRESS`, database `RestaurantManagementDB`; migrations apply on startup.
- **Build gate:** `dotnet build` → 0 errors. If a build fails with *"Cafe.exe is locked"*, an app instance is still running — stop it (`taskkill /F /IM Cafe.exe`) and rebuild.
