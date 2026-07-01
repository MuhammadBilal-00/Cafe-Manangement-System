using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace Cafe.Services
{
    /// <summary>
    /// Phase 10: stores uploaded files under wwwroot/uploads and returns their public relative URL.
    /// Replaces the old "paste a URL" fields for documents / employee records with real binary upload.
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>Saves the file and returns its site-relative URL (e.g. /uploads/xxxx.pdf), or null if empty/invalid.</summary>
        Task<(bool ok, string? url, string? error)> SaveAsync(IFormFile? file, string subFolder = "documents");
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private const long MaxBytes = 15 * 1024 * 1024; // 15 MB
        private static readonly string[] Allowed =
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt",
            ".png", ".jpg", ".jpeg", ".gif", ".webp"
        };

        public FileStorageService(IWebHostEnvironment env) => _env = env;

        public async Task<(bool ok, string? url, string? error)> SaveAsync(IFormFile? file, string subFolder = "documents")
        {
            if (file == null || file.Length == 0) return (false, null, "No file was uploaded.");
            if (file.Length > MaxBytes) return (false, null, "File exceeds the 15 MB limit.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !Allowed.Contains(ext))
                return (false, null, "File type not allowed.");

            // Sanitize the folder name and keep uploads inside wwwroot.
            var safeFolder = new string((subFolder ?? "documents").Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
            if (string.IsNullOrEmpty(safeFolder)) safeFolder = "documents";

            var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var dir = Path.Combine(root, "uploads", safeFolder);
            Directory.CreateDirectory(dir);

            var storedName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, storedName);
            await using (var stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);

            return (true, $"/uploads/{safeFolder}/{storedName}", null);
        }
    }
}
