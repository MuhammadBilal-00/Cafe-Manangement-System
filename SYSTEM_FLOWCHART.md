# System Flowchart — Restaurant Management SaaS

> Reflects the **closed-platform** design currently being applied: no public sign-up, the
> Administrator provisions all users, customers are data-only (no login), **POS is the single
> order-taking screen**, and **Order Management** only tracks/manages orders (its "New Order"
> button redirects to the POS).
>
> View this rendered: open in VS Code (Markdown Preview + Mermaid extension), on GitHub, or paste
> the block below into https://mermaid.live.

```mermaid
flowchart TD
    %% ===================== ACCESS & PROVISIONING =====================
    subgraph ACCESS["🔐 Access &amp; Provisioning — closed platform, no public sign-up"]
        PA["Platform Admin<br/>(SaaS operator)"] -->|provisions| TEN["Tenant / Business<br/>+ Plan &amp; features"]
        TEN --> OWN["Tenant Admin (Owner)"]
        OWN -->|creates, edits,<br/>activates, resets pwd| USERS["Internal Users<br/>Manager · Cashier · Staff · HR · Inventory"]
        USERS --> LOGIN{"Sign In<br/>active internal users only"}
        CUST["Customer = DATA record only<br/>(NO login)"]:::data
    end
    LOGIN --> DASH["Dashboard"]

    %% ===================== SETUP =====================
    subgraph SETUP["⚙️ Setup — Admin / Manager"]
        BR["Branches"]
        MENU["Menu<br/>Categories → Items → Recipes<br/>→ Modifiers / Combos"]
        SUP["Suppliers"] --> PUR["Purchase raw materials"]
        PUR --> STOCK[("Inventory / Stock")]
        TBL["Tables / Floor"]
        KP["Kitchen Printers<br/>+ station routing"]
        CFG["Checkout Settings<br/>tax · auto-KOT · terminal"]
    end
    DASH --> SETUP

    %% ===================== ORDER LIFECYCLE =====================
    subgraph ORDER["🧾 Order Lifecycle"]
        POS["POS — the ONLY order-taking screen<br/>service type · table · items · discounts"]:::pos
        POS -->|creates| ORD["Order + Order Items"]
        ORD -->|deduct atomically| STOCK
        ORD -->|apply| DISC["Promo + Card partnership + Tax"]
        ORD -->|auto-print| KOT["KOT → Kitchen / Bar printer"]
        ORD --> KDS["Kitchen Display (KDS)<br/>live tickets: New→Cooking→Ready"]
        ORD --> PAY{"Payment<br/>Cash · Card · Split"}
        PAY -->|card + terminal| WH["Terminal webhook<br/>→ Paid / Failed"]
        PAY --> INV["Invoice / Bill<br/>A5 PDF · 80mm thermal"]
        INV --> BILL["Bill History"]
        OM["Order Management<br/>track · filter · status · cancel · export"]
        OM -.->|New Order button<br/>redirects to| POS
    end
    DASH --> POS
    DASH --> OM
    MENU --> POS
    TBL --> POS
    CUST -.->|attached at checkout| ORD

    %% ===================== BACK OFFICE =====================
    subgraph BACK["📊 Back office — fed automatically"]
        ACC["Accounting<br/>Journal → Ledger (must balance)"]
        AP["Payables (suppliers)"]
        AR["Receivables (credit sales)"]
        RET["Sell / Purchase Returns"]
        ATT["Attendance"] --> PAYROLL["Payroll"]
        REP["Reports &amp; Dashboard<br/>Revenue − COGS − Expenses = Profit"]
        LOY["Loyalty / Gift cards"]
        CRMx["CRM<br/>customers · leads · campaigns"]
        NOTIF["Notifications<br/>in-app / SignalR · email · SMS"]
        XFER["Stock Transfer / Adjustment / Production"]
    end
    INV --> ACC
    INV -->|credit| AR
    PUR --> AP
    RET --> STOCK
    RET --> ACC
    PAYROLL --> ACC
    ACC --> REP
    POS -.-> LOY
    CUST --> CRMx
    ORD -.-> NOTIF
    STOCK -.->|low stock| NOTIF
    XFER --> STOCK

    classDef pos fill:#d4af37,stroke:#8a6d1a,color:#1e2a3a,stroke-width:2px;
    classDef data fill:#eef2ff,stroke:#8890c8,color:#334;
```

## How to read it
- **POS vs Order Management (your question):** they are **separate**. The **POS** is the one place an
  order is *created*. **Order Management** is a back-office list to *track/manage* existing orders;
  its "New Order" button just opens the POS — it is not a second creation path.
- **Closed platform:** the Platform Admin provisions tenants; each Owner provisions internal users.
  There is no public sign-up, and **customers never log in** — they are data records attached at
  checkout and used by CRM/loyalty/receivables.
- **Solid arrows** = a direct flow/creates; **dotted arrows** = a link/reference or async event.
- Everything downstream of an order (accounting, receivables, loyalty, notifications, reports) is
  fed automatically when the order/payment completes.
