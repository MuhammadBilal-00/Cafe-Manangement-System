// Helpers/SessionExtensions.cs
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Cafe.Helpers
{
    public static class SessionExtensions
    {
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }

        public static int? GetUserId(this ISession session)
        {
            return session.GetInt32("UserId");
        }

        public static string? GetUserRole(this ISession session)
        {
            return session.GetString("UserRole");
        }

        public static string? GetUserName(this ISession session)
        {
            return session.GetString("UserName");
        }

        public static int? GetManagedBranchId(this ISession session)
        {
            return session.GetInt32("ManagedBranchId");
        }

        public static int? GetStaffBranchId(this ISession session)
        {
            return session.GetInt32("StaffBranchId");
        }

        public static bool IsOwner(this ISession session)
        {
            return session.GetUserRole() == "Owner";
        }

        public static bool IsBranchManager(this ISession session)
        {
            return session.GetUserRole() == "BranchManager";
        }

        public static bool IsStaff(this ISession session)
        {
            return session.GetUserRole() == "Staff";
        }

        public static bool IsCustomer(this ISession session)
        {
            return session.GetUserRole() == "Customer";
        }

        public static bool IsAuthenticated(this ISession session)
        {
            return session.GetUserId().HasValue;
        }

        // ── Multi-tenancy (Phase 0) ──

        /// <summary>The tenant the logged-in user belongs to (null for platform admin at the root domain).</summary>
        public static int? GetTenantId(this ISession session)
        {
            return session.GetInt32("TenantId");
        }

        public static bool IsPlatformAdmin(this ISession session)
        {
            return session.GetUserRole() == "PlatformAdmin";
        }

        /// <summary>When a platform admin is impersonating a tenant, the id of that tenant; else null.</summary>
        public static int? GetImpersonatedTenantId(this ISession session)
        {
            return session.GetInt32("ImpersonatingTenantId");
        }

        public static bool IsImpersonating(this ISession session)
        {
            return session.GetImpersonatedTenantId().HasValue;
        }
    }
}