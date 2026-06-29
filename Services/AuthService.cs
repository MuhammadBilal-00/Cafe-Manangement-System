// Services/AuthService.cs
using Cafe.Models;
using Microsoft.EntityFrameworkCore;
using Cafe.Data;

namespace Cafe.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            catch
            {
                return false;
            }
        }

        public User? Authenticate(string email, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null || !VerifyPassword(password, user.PasswordHash ?? string.Empty))
                return null;

            return user;
        }

        public bool HasPermission(User user, string requiredRole)
        {
            if (user == null) return false;

            // Define role hierarchy
            var roleHierarchy = new Dictionary<string, int>
            {
                { "Owner", 4 },
                { "BranchManager", 3 },
                { "Staff", 2 },
                { "Customer", 1 }
            };

            // Check if user's role meets or exceeds required role
            if (roleHierarchy.ContainsKey(user.Role) && roleHierarchy.ContainsKey(requiredRole))
            {
                return roleHierarchy[user.Role] >= roleHierarchy[requiredRole];
            }

            return false;
        }
    }
}