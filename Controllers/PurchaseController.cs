using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;

namespace Cafe.Controllers
{
    [RequireFeature("Purchases")]
    [RequireManagerOrOwner]
    public class PurchaseController : BaseController
    {
        private readonly INotificationService _notificationService;
        private readonly IInventoryService _inventoryService;
        private readonly IMemoryCache _cache;

        // Valid state-machine transitions: key = current status, value = allowed next statuses.
        // Cancelled is terminal — no transitions out.
        private static readonly Dictionary<string, string[]> AllowedTransitions = new()
        {
            ["Pending"]   = ["Approved", "Cancelled"],
            ["Approved"]  = ["Received", "Cancelled"],
            ["Received"]  = ["Cancelled"],
            ["Cancelled"] = []
        };

        public PurchaseController(ApplicationDbContext context, INotificationService notificationService, IInventoryService inventoryService, IMemoryCache cache) : base(context)
        {
            _notificationService = notificationService;
            _inventoryService = inventoryService;
            _cache = cache;
        }

        // GET: Purchase/Index
        public async Task<IActionResult> Index(int? branchId, string? status)
        {
            int? effectiveBranchId = GetEffectiveBranchId(branchId);

            var query = _context.Purchases
                .Include(p => p.Item)
                    .ThenInclude(i => i.Branch)
                .Include(p => p.Supplier)
                .Include(p => p.Branch)
                .Include(p => p.CreatedBy)
                .AsQueryable();

            if (effectiveBranchId.HasValue)
                query = query.Where(p => p.BranchId == effectiveBranchId.Value || p.Item.BranchId == effectiveBranchId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);

            var purchases = await query.OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync();

            // Single aggregated query for KPI status counts — scoped to the same branches as
            // the list below, so a branch manager's tiles never count other branches' orders.
            var countQuery = _context.Purchases.AsQueryable();
            if (effectiveBranchId.HasValue)
                countQuery = countQuery.Where(p => p.BranchId == effectiveBranchId.Value || p.Item.BranchId == effectiveBranchId.Value);
            var statusCounts = await countQuery
                .GroupBy(p => p.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            ViewBag.PendingCount   = statusCounts.GetValueOrDefault("Pending", 0);
            ViewBag.ApprovedCount  = statusCounts.GetValueOrDefault("Approved", 0);
            ViewBag.ReceivedCount  = statusCounts.GetValueOrDefault("Received", 0);
            ViewBag.CancelledCount = statusCounts.GetValueOrDefault("Cancelled", 0);
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effectiveBranchId;
            ViewBag.SelectedStatus = status;

            return View(purchases);
        }

        // POST: Purchase/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var validStatuses = new[] { "Pending", "Approved", "Received", "Cancelled" };
            if (!validStatuses.Contains(newStatus))
                return BadRequest("Invalid status.");

            var purchase = await _context.Purchases
                .Include(p => p.Item)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null) return NotFound();

            // Branch-access guard: BranchManagers may only update their own branch's purchases.
            var purchaseBranchId = purchase.BranchId ?? purchase.Item?.BranchId;
            if (purchaseBranchId.HasValue && !CanAccessBranch(purchaseBranchId.Value))
                return AccessDenied();
            if (!purchaseBranchId.HasValue && GetCurrentUserRole() == "BranchManager")
                return AccessDenied(); // unscoped purchase — only Owner can modify

            var oldStatus = purchase.Status;

            // State-machine guard: reject transitions not in the allowed map.
            if (!AllowedTransitions.TryGetValue(oldStatus, out var allowed) || !allowed.Contains(newStatus))
            {
                SetErrorMessage($"Cannot transition a {oldStatus} purchase to {newStatus}.");
                return RedirectToAction(nameof(Index));
            }

            purchase.Status = newStatus;
            await _context.SaveChangesAsync();

            var performedBy = HttpContext.Session.GetUserName() ?? "System";
            string? stockWarning = null;

            // Approved → Received: credit inventory (atomic, audit-logged).
            if (newStatus == "Received" && purchase.Item != null)
            {
                await _inventoryService.StockIn(purchase.ItemId, purchase.QuantityPurchased,
                    $"Purchase #{purchase.Id} received", performedBy);
            }

            // Received → Cancelled: reverse the inventory credit. If some of that stock was
            // already consumed (e.g. used in orders since it was received), there isn't
            // enough left to reverse — surface that clearly instead of silently clamping to 0.
            if (oldStatus == "Received" && newStatus == "Cancelled" && purchase.Item != null)
            {
                var reversed = await _inventoryService.StockOut(purchase.ItemId, purchase.QuantityPurchased,
                    "Purchase Cancelled", $"Reversal of Purchase #{purchase.Id}", performedBy);
                if (!reversed)
                    stockWarning = " Warning: could not fully reverse stock — some of it has already been used.";
            }

            await _notificationService.CreateNotificationAsync(
                "Purchase Status Updated",
                $"Purchase of {purchase.QuantityPurchased} {purchase.Item?.Name ?? "item"} changed from {oldStatus} to {newStatus}.",
                newStatus == "Cancelled" ? "Warning" : "Info",
                NotificationCategory.Inventory,
                branchId: purchaseBranchId,
                createdBy: GetCurrentUserId(),
                redirectUrl: "/Purchase/Index",
                icon: "fas fa-shopping-cart");

            SetSuccessMessage($"Purchase marked as {newStatus}.{stockWarning}");
            return RedirectToAction(nameof(Index));
        }

        // GET: Purchase/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.Suppliers = await GetAccessibleSuppliers();

            int? scopedBranchId = GetEffectiveBranchId(null);
            var itemQuery = _context.InventoryItems.Include(i => i.Branch).AsQueryable();
            if (scopedBranchId.HasValue) itemQuery = itemQuery.Where(i => i.BranchId == scopedBranchId.Value);
            ViewBag.Items = await itemQuery.OrderBy(i => i.Name).ToListAsync();

            return View(new Purchase { Status = "Pending", DatePurchased = DateTime.Today });
        }

        // POST: Purchase/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Purchase purchase)
        {
            ModelState.Remove("Item");
            ModelState.Remove("Branch");
            ModelState.Remove("Supplier");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("SupplierName");

            // Set SupplierName from supplier if linked
            if (purchase.SupplierId.HasValue && string.IsNullOrEmpty(purchase.SupplierName))
            {
                var supplier = await _context.Suppliers.FindAsync(purchase.SupplierId.Value);
                purchase.SupplierName = supplier?.Name ?? "Unknown";
            }
            if (string.IsNullOrEmpty(purchase.SupplierName))
                purchase.SupplierName = "Manual Entry";

            purchase.CreatedById = GetCurrentUserId();
            purchase.CreatedAt = DateTime.Now;

            if (ModelState.IsValid)
            {
                // Double-click / back-button-resubmit guard: an identical purchase from the
                // same user within a short window is treated as a duplicate, not a new order.
                var dedupeKey = $"purchase-create:{purchase.CreatedById}:{purchase.ItemId}:{purchase.QuantityPurchased}:{purchase.TotalCost}:{purchase.SupplierId}";
                if (_cache.TryGetValue(dedupeKey, out _))
                {
                    SetErrorMessage("This purchase was already submitted moments ago.");
                    return RedirectToAction(nameof(Index));
                }
                _cache.Set(dedupeKey, true, TimeSpan.FromSeconds(15));

                _context.Purchases.Add(purchase);
                await _context.SaveChangesAsync();

                // If created directly as Received, credit inventory immediately.
                if (purchase.Status == "Received")
                {
                    var performedBy = HttpContext.Session.GetUserName() ?? "System";
                    await _inventoryService.StockIn(purchase.ItemId, purchase.QuantityPurchased,
                        $"Purchase #{purchase.Id} received", performedBy);
                }

                SetSuccessMessage("Purchase order created.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.Suppliers = await GetAccessibleSuppliers();
            int? scopedBranchId = GetEffectiveBranchId(null);
            var itemQuery = _context.InventoryItems.Include(i => i.Branch).AsQueryable();
            if (scopedBranchId.HasValue) itemQuery = itemQuery.Where(i => i.BranchId == scopedBranchId.Value);
            ViewBag.Items = await itemQuery.OrderBy(i => i.Name).ToListAsync();
            return View(purchase);
        }
    }
}
