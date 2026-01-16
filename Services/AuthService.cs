// Services/AuthService.cs
using Cafe.Models;
using System.Security.Cryptography;
using System.Text;
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
            // Use SHA256 with salt to match database format
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "SALT_KEY_2025"));
            return Convert.ToBase64String(hashedBytes);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword))
                return false;

            try
            {
                // Generate hash for input password
                var inputHash = HashPassword(password);

                // Compare with stored hash
                return inputHash == hashedPassword;
            }
            catch
            {
                return false;
            }
        }

        public User? Authenticate(string email, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null || !VerifyPassword(password, user.PasswordHash))
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