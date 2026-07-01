using System.Globalization;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>
    /// Guided setup wizard shown after signup, plus CSV import for products and customers.
    /// All writes go through the tenant-scoped DbContext, so imported rows are auto-isolated.
    /// </summary>
    [RequireOwner]
    public class SetupController : BaseController
    {
        public SetupController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            // Wizard progress checklist.
            ViewBag.HasBranchDetails = await _context.Branches.AnyAsync(b => b.Location != "—" && b.Location != "");
            ViewBag.MenuCount = await _context.MenuItems.CountAsync();
            ViewBag.StaffCount = await _context.Staff.CountAsync();
            ViewBag.CategoryCount = await _context.Categories.CountAsync();
            return View();
        }

        // ── CSV import: products ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportProducts(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                SetErrorMessage("Please choose a CSV file.");
                return RedirectToAction(nameof(Index));
            }

            var branch = await _context.Branches.OrderBy(b => b.Id).FirstOrDefaultAsync();
            if (branch == null)
            {
                SetErrorMessage("Create a branch first.");
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.Categories.ToDictionaryAsync(c => c.Name.ToLower(), c => c);
            int created = 0, skipped = 0;

            using var reader = new StreamReader(file.OpenReadStream());
            string? line;
            bool first = true;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = SplitCsv(line);
                // Skip a header row if present.
                if (first && cols.Length > 0 && cols[0].Trim().Equals("name", StringComparison.OrdinalIgnoreCase))
                { first = false; continue; }
                first = false;

                // Expected: Name, Category, Price, [CostPrice]
                if (cols.Length < 3) { skipped++; continue; }
                var name = cols[0].Trim();
                var catName = cols[1].Trim();
                if (name == "" || !decimal.TryParse(cols[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price <= 0)
                { skipped++; continue; }
                decimal cost = 0;
                if (cols.Length >= 4) decimal.TryParse(cols[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out cost);

                if (string.IsNullOrWhiteSpace(catName)) catName = "Uncategorised";
                if (!categories.TryGetValue(catName.ToLower(), out var cat))
                {
                    cat = new Category { Name = catName, Description = catName, IsActive = true, CreatedDate = DateTime.Now };
                    _context.Categories.Add(cat);
                    await _context.SaveChangesAsync(); // need id for FK
                    categories[catName.ToLower()] = cat;
                }

                _context.MenuItems.Add(new MenuItem
                {
                    Name = name,
                    Description = name,
                    Price = price,
                    CostPrice = cost,
                    CategoryId = cat.Id,
                    BranchId = branch.Id,
                    Availability = true,
                    PreparationTime = 10,
                    CreatedDate = DateTime.Now
                });
                created++;
            }
            await _context.SaveChangesAsync();
            SetSuccessMessage($"Imported {created} product(s). {(skipped > 0 ? $"{skipped} row(s) skipped." : "")}");
            return RedirectToAction(nameof(Index));
        }

        // ── CSV import: customers ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCustomers(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                SetErrorMessage("Please choose a CSV file.");
                return RedirectToAction(nameof(Index));
            }

            int created = 0, skipped = 0;
            using var reader = new StreamReader(file.OpenReadStream());
            string? line;
            bool first = true;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = SplitCsv(line);
                if (first && cols.Length > 0 && cols[0].Trim().Equals("name", StringComparison.OrdinalIgnoreCase))
                { first = false; continue; }
                first = false;

                // Expected: Name, Email, [Phone]
                if (cols.Length < 2) { skipped++; continue; }
                var name = cols[0].Trim();
                var email = cols[1].Trim();
                if (name == "" || email == "" || await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email))
                { skipped++; continue; }
                var phone = cols.Length >= 3 ? cols[2].Trim() : "N/A";

                var user = new User { Name = name, Email = email, Phone = string.IsNullOrWhiteSpace(phone) ? "N/A" : phone, Role = "Customer", CreatedDate = DateTime.Now };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                _context.Customers.Add(new Customer { UserId = user.Id, JoinDate = DateTime.Now, IsActive = true });
                created++;
            }
            await _context.SaveChangesAsync();
            SetSuccessMessage($"Imported {created} customer(s). {(skipped > 0 ? $"{skipped} row(s) skipped." : "")}");
            return RedirectToAction(nameof(Index));
        }

        private static string[] SplitCsv(string line)
        {
            // Minimal CSV: handles quoted fields with commas.
            var result = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;
            foreach (var ch in line)
            {
                if (ch == '"') inQuotes = !inQuotes;
                else if (ch == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }
    }
}
