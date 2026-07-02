namespace Cafe.Helpers
{
    /// <summary>
    /// Canonical role names for the closed, admin-provisioned platform.
    /// The tenant Administrator ("Owner") is the only role that creates and manages accounts.
    /// </summary>
    public static class AppRoles
    {
        public const string PlatformAdmin = "PlatformAdmin";
        public const string Owner = "Owner"; // Tenant Admin / Administrator
        public const string BranchManager = "BranchManager";
        public const string Staff = "Staff";
        public const string HR = "HR";
        public const string InventoryManager = "InventoryManager";
        public const string Cashier = "Cashier";

        /// <summary>Customers are business-managed data records — never a login identity.</summary>
        public const string Customer = "Customer";

        /// <summary>Roles the tenant Administrator can assign to internal users.</summary>
        public static readonly string[] AssignableTenantRoles =
            { Owner, BranchManager, Staff, HR, InventoryManager, Cashier };

        /// <summary>
        /// Operational (staff-level) roles: they work at a branch via a Staff record and get
        /// staff-level access. BranchManager/Owner sit above these in the existing hierarchy.
        /// </summary>
        public static readonly string[] StaffLevelRoles = { Staff, HR, InventoryManager, Cashier };

        public static bool IsStaffLevel(string? role) =>
            role != null && StaffLevelRoles.Contains(role);

        /// <summary>Human-friendly label for a role name.</summary>
        public static string Label(string? role) => role switch
        {
            Owner => "Administrator",
            BranchManager => "Branch Manager",
            InventoryManager => "Inventory Manager",
            PlatformAdmin => "Platform Admin",
            _ => role ?? ""
        };
    }
}
