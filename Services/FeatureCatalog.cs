namespace Cafe.Services
{
    /// <summary>
    /// Central list of gateable feature keys. Plans unlock a subset of these (or "*" for all).
    /// Core features are always available regardless of plan so a tenant is never locked out of
    /// the basics. Add new keys here and reference them from [RequireFeature("...")] + the sidebar.
    /// </summary>
    public static class FeatureCatalog
    {
        // Core — always on, even on the Free plan.
        public const string Menu = "Menu";
        public const string Orders = "Orders";
        public const string Branches = "Branches";
        public const string Staff = "Staff";
        public const string Attendance = "Attendance";

        // Gated — must be granted by the tenant's plan.
        public const string Inventory = "Inventory";
        public const string Suppliers = "Suppliers";
        public const string Purchases = "Purchases";
        public const string Payroll = "Payroll";
        public const string Analytics = "Analytics";   // Financials + Reports
        public const string Feedback = "Feedback";
        public const string Marketing = "Marketing";   // Promo codes + card partnerships
        public const string Invoicing = "Invoicing";   // Bill history + PDF invoices
        public const string Kds = "KDS";               // reserved for Phase 1
        public const string Tables = "Tables";         // reserved for Phase 1

        /// <summary>Features available on every plan (including no plan / Free).</summary>
        public static readonly HashSet<string> Core = new(StringComparer.OrdinalIgnoreCase)
        {
            Menu, Orders, Branches, Staff, Attendance
        };

        /// <summary>All gateable feature keys, used to render the plan editor checklist.</summary>
        public static readonly string[] All =
        {
            Inventory, Suppliers, Purchases, Payroll, Analytics,
            Feedback, Marketing, Invoicing, Kds, Tables
        };
    }
}
