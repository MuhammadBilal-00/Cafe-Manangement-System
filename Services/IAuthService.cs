// Services/IAuthService.cs
using Cafe.Models;

namespace Cafe.Services
{
    public interface IAuthService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
        User? Authenticate(string email, string password);
        bool HasPermission(User user, string requiredRole);
    }
}