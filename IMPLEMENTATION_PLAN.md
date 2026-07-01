# Implementation Plan — Multi-Tenant Restaurant SaaS

> **For the engineer/agent picking this up:** This is a self-contained build plan to evolve an existing single-business restaurant app into a **multi-tenant SaaS** that beats a competitor ("Nouvel POS", an UltimatePOS-based system) by miles. Read this whole file first. Build **one phase at a time**, in order. Phase 0 is mandatory and comes before all feature work. Follow the **Standards** section on every task — they are non-negotiable.

---

## 0. Project context (current state — build ON this, don't duplicate)

**Stack:** ASP.NET Core 8 MVC · Entity Framework Core 9 · SQL Server (`Server=BILAL\SQLEXPRESS;Database=RestaurantManagementDB;Trusted_Connection=true`). Razor views. SignalR for real-time. QuestPDF for PDFs. ClosedXML for Excel. BCrypt for passwords.

**Architecture pattern already in place (keep it):**
`Model` → EF migration → `IService`/`Service` (business logic) → `Controller : BaseController` (RBAC + branch scoping) → Razor view using the design system in `wwwroot/css/design-system.css`.

**Auth:** session-based. `AuthService` (BCrypt). Role attributes in `Attributes/`: `[RequireOwner]`, `[RequireManagerOrOwner]`, `[RequireStaffOrAbove]`. `AuthenticationMiddleware` gates non-public paths. Antiforgery is configured with header name `RequestVerificationToken` (AJAX posts send it); the token is rendered once in `_Layout.cshtml`.

**Already built — do not rebuild, extend:**
- Branches, Staff, StaffRoles, Attendance (10 statuses, clock in/out), Payroll/Salary (`SalaryPolicy`, `SalaryRecord`, adjustments, payslips).
- Inventory (`InventoryItem`, `InventoryTransaction`, `InventoryRecipeMapping`), Suppliers, Purchases (PO statuses).
- Menu (`Category`, `MenuItem`, `Ingredient`, `MenuItemIngredient`, `DailySpecial`, `MenuItemReview`).
- Orders (`Order`, `OrderItem`) with a create-order modal in `Views/Order/Index.cshtml`, forward-only status workflow, atomic inventory deduction, idempotency guard.
- **Checkout suite (recent):** `PromoCode` (+admin CRUD), `Partnership` (bank/card discounts +CRUD), `Invoice` + **Bill History** (`InvoiceController`), `BranchSetting` (tax rate, hardware-terminal toggle, invoice footer), `CheckoutSettingsController`, `PaymentWebhookController` (generic terminal webhook, shared-secret auth), `CheckoutPricingService` (promo+partnership+tax stacking), `PdfInvoiceService` (QuestPDF), `InvoiceService`.
- Financial dashboard (revenue, **COGS**, expenses, profit), Reports, Expenses, Feedback, Notifications (SignalR `NotificationHub` + `EmailQueue` background worker), Audit logs, Users, Todo.
- Dark/light theme via CSS variables. Design system: `kpi-card`, `premium-table`, `card/card-body/card-body-flush`, `badge-success/warning/danger/info/neutral/accent`, `form-row/form-group/form-label/form-control/form-select`, `btn-primary/secondary/ghost/danger/sm/lg`, `divider`, `ActionCard` (toasts/confirm/prompt in `wwwroot/js/actioncard.js`).

**Known repo quirks (respect these):**
- The app **auto-applies migrations on startup** (`Program.cs`). Keep migrations **additive**.
- SQL Server rejects multiple cascade paths — set new FK delete behavior to **`NoAction`** (required FKs) or **`SetNull`** (optional FKs). Never let EF generate cascades into `Branches`/`Orders`/`Users`/`Tenants`.
- Currency is PKR, displayed as `Rs.`.
- Back up / confirm before any `database update` that isn't purely additive.

---

## 1. The goal

Turn this into a **multi-tenant SaaS restaurant platform** sellable to many cafe/restaurant clients, that is **measurably better than Nouvel/UltimatePOS-class systems** on UX, real-time, reliability, offline capability, and AI. Deliver the 62-item feature backlog (Appendix A) on top of a tenant-isolated foundation.

---

## 2. Standards — apply to EVERY task (non-negotiable)

### 2a. Engineering ("super effective code")
- **Async everywhere** (`async/await`, `...Async()`); no blocking calls.
- **Atomic DB writes for money/stock** — conditional SQL updates (pattern: `UPDATE ... SET qty = qty - @x WHERE id=@id AND qty >= @x`) or transactions. Never read-then-write on contended rows.
- **No N+1** — project with `.Select()`, batch with `.GroupBy()`/dictionaries, deliberate `.Include()`, **paginate every list** server-side.
- **Indexes + constraints** — unique indexes on natural keys; check constraints for status/enum columns; FK delete `NoAction`/`SetNull`.
- **DTOs at the edges** — validate `[FromBody]`/form requests with data annotations + `ModelState.IsValid`; never bind raw entities for money fields; **re-validate server-side** anything a client could tamper with.
- **Thin controllers**, logic in services behind interfaces.
- **Heavy work off the request thread** — reports/exports/PDF/SMS/AI via background worker or queue (follow `EmailBackgroundWorker`).
- **Caching** for rarely-changing reference data via `IMemoryCache`, invalidated on write.
- **Security every action**: a role/feature attribute, `[ValidateAntiForgeryToken]` on every POST, audit logging on money/stock/role/tenant changes.
- **One additive migration per phase**, reviewed before applying.
- **Build gate**: `dotnet build` must be 0 errors before moving on.

### 2b. UI/UX ("no compromise")
- **Reuse the design system only.** Any genuinely new pattern gets added once to `design-system.css` and reused — never copy-pasted inline.
- **Four states per screen:** loading (skeleton), empty (icon + helper + CTA), error (friendly + retry), content.
- **Dark-mode parity** — CSS variables only, no hard-coded colors.
- **Responsive** — `repeat(auto-fit,minmax(...))` grids, horizontal-scroll table wrappers.
- **Consistent feedback** — `ActionCard` + `TempData`, submit spinners, inline validation.
- **POS/KDS are touch-first** — big targets, tile grids, minimal typing, keyboard shortcuts.
- **Accessibility** — labels, focus states, sufficient contrast in both themes.

### 2c. Per-phase definition of done
Schema migrated (additive) · services + controllers + views complete · RBAC + antiforgery + validation in place · no N+1, lists paginated · `dotnet build` clean · **browser-verified** end-to-end (create→act→assert in DB + UI) · committed with a descriptive message.

---

## 3. Architecture decision — multi-tenancy

**Model: shared database + `TenantId` column + EF Core global query filters.** (Best for many SMB tenants; cheap to operate; easy cross-tenant analytics. Escape hatch: move a large client to a dedicated DB later via per-tenant connection string.)

Hierarchy: **Tenant (Business) → Branches → all operational data.** The current "Owner" role becomes **Tenant Admin**. A new **Platform Admin** role sits above all tenants (the SaaS operator).

---

## PHASE 0 — Multi-tenant foundation (DO FIRST, gates everything)

**Goal:** every tenant-owned row is automatically isolated; tenants self-onboard; plans gate features; you (platform admin) manage tenants.

### 0.1 Schema
- `Tenant` (Id, Name, Slug [unique, subdomain], CustomDomain?, Status [Active/Suspended/Trial], PlanId?, BrandingJson, CreatedAt).
- `Plan` (Id, Name, PriceMonthly, MaxBranches, MaxUsers, FeaturesJson/flags).
- `Subscription` (Id, TenantId, PlanId, Status, CurrentPeriodStart/End, Provider, ExternalRef).
- Add `TenantId` (nullable first, then required after backfill) to **every existing tenant-owned entity** (Branch, User, Staff, MenuItem, Order, Invoice, InventoryItem, Supplier, Purchase, Expense, PromoCode, Partnership, BranchSetting, Attendance, SalaryRecord, Notification, Feedback, Category, Ingredient, etc.). Platform-admin users have `TenantId = null`.
- Introduce a base interface `ITenantOwned { int TenantId }` and apply by convention.

### 0.2 Isolation (safety-critical)
- **`ITenantContext`** (scoped service) holding current `TenantId` (and null for platform admin).
- **Global query filter** on every `ITenantOwned` entity in `OnModelCreating`: `HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`. Apply via reflection/convention so none is missed.
- **`SaveChanges` interceptor** stamps `TenantId` on insert automatically.
- **Tenant resolution middleware** (runs before auth gate): resolve tenant from **subdomain** (`{slug}.yourbrand.com`) or custom domain; set `ITenantContext`. API requests resolve from a tenant claim/header.
- **IDOR tests** (xUnit): assert tenant A cannot read/write tenant B rows through any service. Required before Phase 1.

### 0.3 Roles & platform console
- Add **Platform Admin** role + `[RequirePlatformAdmin]` attribute. Separate console area (`/platform`) — list/create/suspend tenants, manage plans, **support impersonation** (assume a tenant session, fully audit-logged), usage metrics.
- Tenant Admin = today's "Owner", scoped to its tenant.

### 0.4 Plans & feature gating
- `[RequireFeature("KDS")]` action attribute + a `IFeatureGate` service that reads the tenant's plan. Sidebar hides modules not in the plan; server guards enforce it too.
- **`IBillingProvider`** abstraction: `StripeBillingProvider` (international) + `ManualBillingProvider` (local/Pakistan invoicing). Plan gating works regardless of provider wired.

### 0.5 Onboarding & provisioning
- **Self-serve signup** → `TenantProvisioningService` creates: tenant, default branch, roles, chart-of-accounts seed (placeholder until Phase 5), units, a **"Walk-In Customer"**, a **starter template** (cafe/restaurant/bakery sample menu + categories), and the admin user. Send welcome email via existing queue.
- **Setup wizard** + CSV import for products/customers.

### 0.6 Per-tenant branding (white-label)
- Logo, brand colors (override CSS variables — same mechanism as the theme), business name, receipt header/footer, custom domain. Self-serve in tenant settings.

### 0.7 Retrofit migration (do carefully)
1. Add `Tenant`/`Plan`/`Subscription` + nullable `TenantId` everywhere.
2. Backfill all existing rows to one "Demo" tenant.
3. Make `TenantId` required; add indexes `(TenantId, ...)` on hot queries.
4. Enable global filters + interceptor + resolution middleware.
5. Add platform console + signup/provisioning.

**Verify:** create two tenants; confirm each sees only its own data through the UI and API; impersonation works and is audited; signup provisions a working tenant; feature gating hides a plan-locked module.

---

## PHASES 1–9 — feature build (each is tenant-aware automatically via Phase 0)

> For every phase: schema (additive migration) → service(s) → controller(s) → view(s) using the design system → effectiveness pass → browser verify → commit.

### PHASE 1 — POS & Restaurant Core  *(items 1,2,3,4,6,7,9,10,11,12,13,14,15,16,17,19)*
**Schema:** `Table` (BranchId, Name, Capacity, Zone, Status). `Order` += `TableId?`, `ServiceType` (DineIn/Takeaway/Delivery), `ServiceStaffId?`, `KitchenStatus` (New/Cooking/Ready/Served), `HoldState` (Active/Suspended/Draft); **make `CustomerId` nullable**. `OrderItem` += `LineDiscount`, `SentToKitchen`, `Notes`. New `Payment` table (InvoiceId, Method, Amount, Reference, PaidAt) for **split payments**; `Invoice.PaymentStatus` derives from sum(payments). Seed **"Walk-In Customer"** per tenant.
**Services:** `IPosService` (cart, totals via extended `CheckoutPricingService` with packing/shipping/line-discount/editable-tax, hold/resume/draft, finalize with split payments — atomic + idempotent). `IKitchenService` (ticket feed, status transitions, per-item routing). `ITableService`.
**Controllers/endpoints:** extend Order/new `PosController` (`Hold`, `Resume`, `SaveDraft`, `AddPayment`, `QuickExpense`, `RecentTransactions`, `ScanLookup`), `KitchenController` (`Index` KDS, `UpdateTicketStatus`, SignalR feed), `TableController` (+ floor map).
**UI:** **Register redesign** (touch-first): cart w/ per-line qty/discount/remove; right = Category/Brand **tile grid** with stock badges + barcode/search; bottom bar Cash/Card/**Multiple Pay**/Suspend/Draft/Quotation; service-type toggle, table picker, service-staff picker; packing/shipping/tax/discount lines; Recent Transactions drawer; Add-Expense modal. **Kitchen Display** card board, color-coded by age, sound on new order, **SignalR live** (no polling). **Floor/Tables** visual grid. New sidebar entries under Operations.
**Effectiveness:** split-payment finalize in one tx; KDS via SignalR push; barcode lookup indexed on SKU; table status atomic.
**Beats Nouvel:** real-time KDS (theirs has a Refresh button), optional walk-in, modern touch register.

### PHASE 2 — Menu & Product Depth  *(23,24,25,27,28,29,26)*
**Schema:** `Brand`, `Unit` (+conversions), `ModifierGroup`/`Modifier` (+ `MenuItemModifierGroup`), `Combo`/`ComboItem`, `MenuItem` += availability window + day mask, `PriceGroup`/tiered pricing, optional serial/warranty.
**UI/Services:** `BrandController`, `UnitController`, `ModifierController`, `ComboController` (CRUD mirroring `SupplierController`); POS **modifier picker modal**; availability filtered in the DB query; combo expands to components for inventory deduction; modifier price math server-side.

### PHASE 3 — Inventory & Supply Chain  *(20,21,22)*
**Schema:** `StockTransfer`/`StockTransferItem` (From/To branch, status); `StockAdjustment`/lines (reason, type, approval); `ProductionOrder`/inputs/outputs using `InventoryRecipeMapping` with **manufactured-cost roll-up**.
**Effectiveness:** all stock moves go through the atomic `InventoryService` (already audit-logged); transfer is one transaction across both branches.

### PHASE 4 — Sales Lifecycle, Returns, Receivables  *(5,8,33,34,35,36)*
**Schema:** `Quotation`/items (convertible to order); `SellReturn`/`PurchaseReturn`/lines (restock + ledger); customer **due (AR)** + supplier **due (AP)** balances driven by credit sales (#5) and partial `Payment`s; `TaxGroup`/`Tax` (multiple/compound) replacing the single branch rate.
**UI:** Credit Sale on POS (invoice Pending + receivable); Due dashboards; return wizards.
**Effectiveness:** balances via indexed aggregates, cached per customer, invalidated on payment.

### PHASE 5 — Accounting  *(30,31,32,37)*
**Schema (heaviest):** `Account` (chart of accounts, hierarchical, Asset/Liability/Equity/Income/Expense), `JournalEntry`/`JournalLine` (debit/credit, **must balance** — enforced in service + check constraint), `PaymentAccount` (bank/cash registers + reconciliation), `Budget`/lines. **Auto-posting**: sales/purchases/payroll/expenses/returns post journals automatically via a domain-event hook (keeps modules decoupled, idempotent per source doc). Reports: Trial Balance, P&L, Balance Sheet as set-based SQL. **#37**: `ITaxInvoiceProvider` pluggable adapter (region-specific e-invoicing, e.g. FBR/PRA) behind a tenant setting; ship a stub + one real adapter.

### PHASE 6 — Customer Portal & Marketing  *(38,39,40,41,42,43,44,45)*
**Architecture:** separate customer-facing area — `_CustomerLayout`, controllers under `/shop` or `/account`, `[RequireCustomer]`. Reuse design system, lighter public shell.
**Features:** customer login/register (exists — give it a real home), **menu browse + online order** (feeds the same Order/KDS pipeline as Delivery/Takeaway), **order history + live tracking** (SignalR). `LoyaltyTransaction` (earn on paid invoice / redeem at checkout — the existing `LoyaltyPoints` field finally gets a flow). `GiftCard` (code/balance, as a payment method). CRM: `Lead`/`FollowUp`/`Campaign`/segments. `NotificationTemplate` (channel + tokenized body — notifications/emails render from these). **Catalogue QR** (public read-only menu per branch). `ISmsProvider` adapter (queued like email).
**Effectiveness:** public endpoints rate-limited + hard-cached; online orders reuse the atomic order pipeline; loyalty/gift-card balance changes atomic.

### PHASE 7 — HR Depth  *(46,47,48,49,50)* — parallelizable
`LeaveType`/`LeaveRequest` (+approval; **deducts via the existing salary engine**), `Holiday` calendar (feeds attendance), `Department`/`Designation` (FK on Staff), `SalesTarget`/`Commission`, `EmployeeDocument`. Mirror existing Staff/Attendance/Payroll patterns.

### PHASE 8 — Productivity / Essentials  *(51,52,53,54,55)* — parallelizable
`Document`, `Memo`, `Reminder` (+notification), `Message` (internal DM via SignalR), `KnowledgeBaseArticle`. Simple CRUD + existing components; an "Essentials" sidebar group with tabs.

### PHASE 9 — Platform & System  *(56,57,58,59,60,61,62)*
- **i18n (#56):** `.resx` + `IStringLocalizer`, culture cookie + switcher in `_Layout`; extract hard-coded strings (mechanical but large).
- **#57** SaaS tenancy — delivered in Phase 0.
- **Thermal receipt (#58):** 80mm template alongside the A5 PDF (template strategy in `PdfInvoiceService`).
- **Export everywhere (#59):** one reusable `IExportService` (CSV/Excel(ClosedXML)/PDF) + a shared export-button partial dropped into every `premium-table`.
- **POS/receipt settings (#60):** extend `BranchSetting`/new `PosProfile`.
- **Delivery/rider (#61):** `Rider`/`Delivery` (assign, track) extending the order pipeline.
- **Shipments (#62):** `Shipment` linked to orders/transfers.

---

## 4. Differentiation workstreams — run ACROSS phases (this is how it beats Nouvel by miles)

1. **Reliability as a feature** — every stock/money path atomic; double-entry that must balance; idempotent everything. (The competitor's live data showed stock at −2,700+. Don't have that bug.)
2. **Real-time, not Refresh** — SignalR for KDS, order status, table floor, live dashboards.
3. **Offline-capable PWA** — installable POS + KDS that keep taking orders offline (IndexedDB queue) and sync on reconnect; customer ordering PWA. *(Schedule alongside Phase 1 register + Phase 6 portal.)*
4. **AI layer** — `IInsightsService` (Anthropic Claude API): natural-language analytics ("how were Friday dinners last month?"), demand forecasting + smart reorder, menu engineering (stars/dogs), anomaly/wastage detection, AI-assisted onboarding (structure a messy menu import). Build incrementally from Phase 3 (forecasting) and Phase 5 (analytics).
5. **Modern UX & speed** — keep the dark-mode premium design system; sub-second pages via the perf standards.
6. **Open platform** — clean REST API + webhooks (extend the existing webhook pattern), public docs, third-party delivery integrations (foodpanda/Uber Eats), Zapier.
7. **Fast time-to-value** — self-serve signup, starter templates, guided setup, import (Phase 0).

**Positioning:** *"A modern, real-time, AI-assisted restaurant cloud that works offline and never corrupts your stock or your books — set up in an afternoon."*

---

## 5. Suggested milestones
- **M1:** Phase 0 (tenancy) + Phase 1 (POS/KDS/tables) — *now it's a real multi-tenant restaurant POS.*
- **M2:** Phases 2–3 (menu depth, inventory/manufacturing).
- **M3:** Phase 4 (returns, receivables).
- **M4:** Phase 5 (accounting).
- **M5:** Phase 6 (portal, loyalty, marketing) + PWA/AI workstreams maturing.
- **M6:** Phases 7–8 (HR, essentials, parallel).
- **M7:** Phase 9 (i18n, delivery, polish).

---

## 6. Execution protocol (how to work through this)
- Build **one phase at a time**, in order. **Phase 0 first.**
- Start each phase by re-reading Section 2 (Standards).
- Keep migrations **additive**; FK delete behavior `NoAction`/`SetNull`; back up before non-additive changes; confirm before any destructive DB op.
- **Browser-verify** each phase end-to-end before committing.
- **Commit per phase** with a clear message; never skip the `dotnet build` 0-error gate.
- If a decision is genuinely ambiguous (e.g., billing provider, e-invoicing region), ask the owner rather than guessing.
- Update the checklist in Appendix A as items land.

---

## Appendix A — 62-item backlog (tick as completed)

### A. POS / Register
- [ ] 1 ⭐ Optional customer / Walk-In default
- [ ] 2 Barcode / SKU scanning
- [ ] 3 Category & Brand product tile grid with stock badges
- [ ] 4 Split / multiple payment
- [ ] 5 Credit sale (→ customer due)
- [ ] 6 Suspend / hold order
- [ ] 7 Draft orders
- [ ] 8 Quotations
- [ ] 9 Service type: Dine-in / Takeaway / Delivery
- [ ] 10 Packing + shipping charge lines
- [ ] 11 Per-line (item-level) discounts
- [ ] 12 Editable order tax at register
- [ ] 13 Add Expense from POS
- [ ] 14 Recent Transactions panel

### B. Restaurant Operations
- [ ] 15 Kitchen Display System (KDS)
- [ ] 16 Tables / floor management
- [ ] 17 Service-staff assignment
- [ ] 18 Bookings / table reservations
- [ ] 19 Per-item kitchen routing

### C. Inventory & Supply Chain
- [ ] 20 Stock transfers between branches
- [ ] 21 Dedicated Stock Adjustment module
- [ ] 22 Manufacturing / Production + cost roll-up
- [ ] 23 Brands
- [ ] 24 Units of measure + conversions
- [ ] 25 Multiple price groups / tiers
- [ ] 26 Warranties / serial tracking

### D. Menu / Products
- [ ] 27 Modifiers / add-ons / variations
- [ ] 28 Combo / deal meals
- [ ] 29 Time-based menu availability

### E. Finance & Accounting
- [ ] 30 Double-entry accounting (CoA, journal, ledger, trial balance)
- [ ] 31 Budgets
- [ ] 32 Payment Accounts (bank/cash + reconciliation)
- [ ] 33 Sell Returns
- [ ] 34 Purchase Returns
- [ ] 35 Customer (AR) + Supplier (AP) due balances
- [ ] 36 Tax groups / multiple taxes
- [ ] 37 Government tax e-invoicing (PRA/FBR-style)

### F. Customers, Portal & Marketing
- [ ] 38 ⭐ Customer-facing portal / online ordering / login experience
- [ ] 39 Order tracking for customers
- [ ] 40 CRM (leads, follow-ups, campaigns)
- [ ] 41 Loyalty points program
- [ ] 42 Gift cards / vouchers
- [ ] 43 Editable notification templates
- [ ] 44 Catalogue QR / digital menu
- [ ] 45 SMS / WhatsApp integration

### G. People / HR
- [ ] 46 Leave management + approvals
- [ ] 47 Holidays calendar
- [ ] 48 Departments & designations
- [ ] 49 Sales targets / commissions
- [ ] 50 Employee documents

### H. Productivity / Essentials
- [ ] 51 Documents
- [ ] 52 Memos
- [ ] 53 Reminders
- [ ] 54 Internal messaging
- [ ] 55 Knowledge base

### I. Platform / System
- [ ] 56 Multi-language (i18n)
- [ ] 57 Multi-business / SaaS tenancy  *(Phase 0)*
- [ ] 58 Thermal / 80mm receipt printing + barcode
- [ ] 59 Consistent export everywhere
- [ ] 60 Configurable POS & receipt settings
- [ ] 61 Delivery / rider management
- [ ] 62 Shipments

---

## Appendix B — Differentiators (cross-cutting, not in the 62)
- [ ] Real-time everywhere (SignalR: KDS, order status, dashboards)
- [ ] Offline-capable PWA (POS + KDS + customer app)
- [ ] AI layer (Claude API: NL analytics, forecasting, menu engineering, anomaly detection, AI onboarding)
- [ ] Public REST API + webhooks + integrations marketplace
- [ ] Per-tenant white-label branding + custom domains
- [ ] Self-serve onboarding with starter templates + import

*End of plan.*
