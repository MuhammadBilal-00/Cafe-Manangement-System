using System.Threading.Tasks;

namespace Cafe.Services
{
    public interface IEmailService
    {
        /// <summary>Send a single email via SMTP.</summary>
        Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);
    }
}
