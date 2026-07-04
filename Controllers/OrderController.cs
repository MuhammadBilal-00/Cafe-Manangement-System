using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using Cafe.Data;
using Cafe.Models;
using Cafe.Attributes;
using Cafe.Helpers;
using Cafe.Services;
using System.Text.Json;
using Cafe.Models.Requests;

namespace Cafe.Controllers
{
    [RequireStaffOrAbove]
    public class OrderController : BaseController
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<OrderController> _logger;
        private readonly IExportService _export;
        private readonly IKitchenService _kitchen;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<Cafe.Hubs.KitchenHub> _kitchenHub;

        public OrderController(ApplicationDbContext context, INotificationService notificationService,
            ILogger<OrderController> logger, IExportService export, IKitchenService kitchen,
            Microsoft.AspNetCore.SignalR.IHubContext<Cafe.Hubs.KitchenHub> kitchenHub) : base(context)
        {
            _notificationService = notificationService;
            _logger = logger;
            _export = export;
            _kitchen = kitchen;
            _kitchenHub = kitchenHub;
        }

        /// <summary>Push the order's kitchen ticket to every KDS watching its branch so the
        /// boards reflect Order Management changes instantly (best-effort, never blocks).</summary>
        private async Task PushTicketToKdsAsync(int orderId, int branchId)
        {
            try
            {
                var ticket = await _kitchen.GetTicketAsync(orderId);
                if (ticket != null)
                    await _kitchenHub.Clients.Group($"kitchen_branch_{branchId}")
                        .SendAsync("TicketUpdated", ticket);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "KDS push failed for order {OrderId}", orderId); }
        }

        // Phase 9 (59): Excel export via the reusable IExportService.
        public async Task<IActionResult> ExportExcel(int? branchId, string? status, DateTime? from, DateTime? to)
        {
            var query = _context.Orders.Include(o => o.Customer).Include(o => o.Branch).AsQueryable();
            if (!HttpContext.Session.IsOwner())
            {
                var ids = (await GetAccessibleBranches()).Select(b => b.Id).ToList();
                query = query.Where(o => ids.Contains(o.BranchId));
            }
            else if (branchId.HasValue) query = query.Where(o => o.BranchId == branchId.Value);
            if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
            if (from.HasValue) query = query.Where(o => o.OrderDate >= from.Value);
            if (to.HasValue) query = query.Where(o => o.OrderDate <= to.Value.AddDays(1));

            var orders = await query.OrderByDescending(o => o.OrderDate).Take(5000).ToListAsync();
            var headers = new[] { "Order #", "Date", "Customer", "Branch", "Service", "Status", "Amount" };
            var rows = orders.Select(o => new object?[] { o.OrderNumber, o.OrderDate, o.Customer?.Name ?? "Walk-In", o.Branch?.Name, o.ServiceType, o.Status, o.TotalAmount });
            var bytes = _export.ToExcel("Orders", headers, rows);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"orders-{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // Main Index Page - Works with your HTML
        public async Task<IActionResult> Index(int? branchId, string status, string search, DateTime? orderDate)
        {
            await PopulateViewBagData(branchId);
            return View("Index");
        }

        // ===== API ENDPOINTS FOR YOUR HTML =====

        // Get Orders as JSON for your JavaScript table
        [HttpGet]
        public async Task<IActionResult> GetOrders(int? branchId, string status, string search, DateTime? orderDate, int page = 1, int pageSize = 10)
        {
            var ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .Include(o => o.OrderItems)
                .AsQueryable();

            // Apply role-based filtering
            ordersQuery = ApplyRoleBasedFiltering(ordersQuery, branchId);

            // Apply filters
            if (!string.IsNullOrEmpty(status) && status != "all-orders")
            {
                var statusFilter = MapStatusFromTab(status);
                ordersQuery = ordersQuery.Where(o => o.Status == statusFilter);
            }

            if (!string.IsNullOrEmpty(search))
            {
                ordersQuery = ordersQuery.Where(o =>
                    o.OrderNumber.Contains(search) ||
                    o.Customer.Name.Contains(search) ||
                    o.Customer.Email.Contains(search));
            }

            if (orderDate.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.OrderDate.Date == orderDate.Value.Date);
            }

            // Get total count
            var totalCount = await ordersQuery.CountAsync();

            // Apply pagination and get results
            var orders = await ordersQuery
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    id = o.Id,
                    orderNumber = o.OrderNumber,
                    customerName = o.Customer != null ? o.Customer.Name : "Walk-In",
                    customerPhone = o.Customer != null ? o.Customer.Phone : null,
                    branchName = o.Branch.Name,
                    orderDate = o.OrderDate.ToString("yyyy-MM-dd HH:mm"),
                    totalAmount = o.TotalAmount,
                    status = o.Status,
                    itemCount = o.OrderItems.Count(),
                    notes = o.Notes
                })
                .ToListAsync();

            return Json(new
            {
                orders = orders,
                totalCount = totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                currentPage = page
            });
        }

        // Get Order Details for View Modal
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null || !CanAccessBranch(order.BranchId))
            {
                return NotFound();
            }

            var orderDetails = new
            {
                id = order.Id,
                orderNumber = order.OrderNumber,
                customer = new
                {
                    name = order.Customer?.Name ?? "Walk-In",
                    email = order.Customer?.Email,
                    phone = order.Customer?.Phone
                },
                branch = order.Branch.Name,
                orderDate = order.OrderDate.ToString("yyyy-MM-dd HH:mm"),
                status = order.Status,
                notes = order.Notes,
                items = order.OrderItems.Select(oi => new
                {
                    name = oi.MenuItem.Name,
                    quantity = oi.Quantity,
                    unitPrice = oi.Price,
                    subtotal = oi.Quantity * oi.Price,
                    description = oi.MenuItem.Description
                }).ToList(),
                // No tax/service charge is actually applied anywhere in order creation —
                // subtotal must equal totalAmount so the receipt math is never inconsistent
                // with what was actually charged.
                subtotal = order.TotalAmount,
                tax = 0m,
                serviceCharge = 0m,
                totalAmount = order.TotalAmount
            };

            return Json(orderDetails);
        }

        // Get Status Counts for Tabs
        [HttpGet]
        public async Task<IActionResult> GetStatusCounts(int? branchId)
        {
            var query = _context.Orders.AsQueryable();
            query = ApplyRoleBasedFiltering(query, branchId);

            var counts = new
            {
                all = await query.CountAsync(),
                pending = await query.CountAsync(o => o.Status == "Pending"),
                preparing = await query.CountAsync(o => o.Status == "Preparing"),
                ready = await query.CountAsync(o => o.Status == "Ready"),
                completed = await query.CountAsync(o => o.Status == "Completed"),
                cancelled = await query.CountAsync(o => o.Status == "Cancelled")
            };

            return Json(counts);
        }

        // NOTE: order CREATION lives in the POS register (PosController.Finalize) — the single
        // source of truth. The old duplicate create-order endpoint + modal were retired when
        // Order Management became a tracking-only screen.

        // Update Order Status
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateStatusRequest request)
        {
            try
            {
                if (request == null || !ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Invalid status update request." });
                }

                var order = await _context.Orders.FindAsync(request.OrderId);
                if (order == null || !CanAccessBranch(order.BranchId))
                {
                    return Json(new { success = false, message = "Order not found or access denied" });
                }

                // Validate status transition (central forward-only workflow)
                if (!OrderWorkflow.CanTransitionOrder(order.Status, request.NewStatus))
                {
                    return Json(new { success = false, message = "Invalid status transition" });
                }

                order.Status = request.NewStatus;
                // Pull the kitchen ticket forward too (Ready → Ready, Completed → Served) so
                // the KDS and Order Management never disagree — one atomic SaveChanges.
                OrderWorkflow.SyncKitchenFromOrder(order);
                await _context.SaveChangesAsync();
                await PushTicketToKdsAsync(order.Id, order.BranchId);

                // Notification: order status changed
                await _notificationService.CreateNotificationAsync(
                    "Order Status Updated",
                    $"Order #{order.OrderNumber} status changed to {request.NewStatus}.",
                    request.NewStatus == "Completed" ? "Success" : "Info",
                    NotificationCategory.Order,
                    branchId: order.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: $"/Order/Index",
                    icon: "fas fa-arrows-rotate");

                return Json(new { success = true, message = $"Order status updated to {request.NewStatus}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for order {OrderId}", request?.OrderId);
                return Json(new { success = false, message = "Something went wrong while updating the order status. Please try again." });
            }
        }

        // Search Customers for autocomplete
        [HttpGet]
        public async Task<IActionResult> SearchCustomers(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Json(new List<object>());

            var customers = await _context.Customers
                .Where(c => c.IsActive)
                .Include(c => c.User)
                .Where(c => c.User.Name.Contains(term) || c.User.Email.Contains(term) || c.User.Phone.Contains(term))
                .Select(c => new
                {
                    id = c.User.Id,
                    name = c.User.Name,
                    email = c.User.Email,
                    phone = c.User.Phone
                })
                .OrderBy(u => u.name)
                .Take(10)
                .ToListAsync();

            return Json(customers);
        }

        // Quick Create Customer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCreateCustomer([FromBody] QuickCustomerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Phone))
                    return Json(new { success = false, message = "Name and Phone are required." });

                // Check for existing user with same phone
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Phone == request.Phone);
                if (existingUser != null)
                    return Json(new { success = false, message = "A user with this phone number already exists." });

                var user = new User
                {
                    Name = request.Name,
                    Email = request.Email ?? $"{request.Phone}@customer.local",
                    Phone = request.Phone,
                    Role = "Customer",
                    PasswordHash = null, // data-only record — customers never log in
                    CreatedDate = DateTime.Now
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var customer = new Customer
                {
                    UserId = user.Id,
                    IsActive = true,
                    JoinDate = DateTime.Now
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = user.Id, name = user.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quick customer");
                return Json(new { success = false, message = "Something went wrong while creating the customer. Please try again." });
            }
        }

        // Get Branches for dropdown
        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var branches = await GetAccessibleBranches();
            var branchList = branches.Select(b => new
            {
                id = b.Id,
                name = b.Name,
                location = b.Location
            }).ToList();

            return Json(branchList);
        }

        // Cancel Order
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> CancelOrder([FromBody] CancelOrderRequest request)
        {
            try
            {
                if (request == null || !ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Invalid cancel request." });
                }

                var order = await _context.Orders.FindAsync(request.OrderId);
                if (order == null || !CanAccessBranch(order.BranchId))
                {
                    return Json(new { success = false, message = "Order not found or access denied" });
                }

                if (order.Status == "Completed")
                {
                    return Json(new { success = false, message = "Cannot cancel completed orders" });
                }

                order.Status = "Cancelled";
                // Close the kitchen ticket so the KDS drops it immediately (nothing to cook).
                OrderWorkflow.SyncKitchenFromOrder(order);
                order.Notes = string.IsNullOrEmpty(order.Notes)
                    ? $"Cancelled: {request.Reason}"
                    : $"{order.Notes}\nCancelled: {request.Reason}";

                await _context.SaveChangesAsync();
                await PushTicketToKdsAsync(order.Id, order.BranchId);

                // Notification: order cancelled
                await _notificationService.CreateNotificationAsync(
                    "Order Cancelled",
                    $"Order #{order.OrderNumber} has been cancelled. Reason: {request.Reason}",
                    "Warning", NotificationCategory.Order,
                    branchId: order.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: $"/Order/Index",
                    icon: "fas fa-ban");

                return Json(new { success = true, message = "Order cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", request?.OrderId);
                return Json(new { success = false, message = "Something went wrong while cancelling the order. Please try again." });
            }
        }

        // Print bill for an order. There is exactly ONE receipt implementation — the
        // thermal invoice PDF — so this resolves the order's bill and hands off to it.
        // (The old View("Receipt") had no view behind it and 500'd.)
        [HttpGet]
        public async Task<IActionResult> GenerateReceipt(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null || !CanAccessBranch(order.BranchId))
            {
                return NotFound();
            }

            var invoiceId = await _context.Invoices
                .Where(i => i.OrderId == id)
                .OrderByDescending(i => i.Id)
                .Select(i => (int?)i.Id)
                .FirstOrDefaultAsync();

            if (invoiceId == null)
            {
                return Content("No bill has been generated for this order yet. Bills are created when the sale is finalized at the POS register.");
            }

            return RedirectToAction("Thermal", "Invoice", new { id = invoiceId.Value });
        }

        // ===== HELPER METHODS =====

        private IQueryable<Order> ApplyRoleBasedFiltering(IQueryable<Order> query, int? branchId)
        {
            if (!HttpContext.Session.IsOwner())
            {
                var userBranchId = HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();

                if (userBranchId.HasValue)
                {
                    query = query.Where(o => o.BranchId == userBranchId.Value);
                }
            }
            else if (branchId.HasValue)
            {
                query = query.Where(o => o.BranchId == branchId.Value);
            }

            return query;
        }

        private string MapStatusFromTab(string tabStatus)
        {
            return tabStatus switch
            {
                "pending-orders" => "Pending",
                "preparing-orders" => "Preparing",
                "ready-orders" => "Ready",
                "completed-orders" => "Completed",
                "cancelled-orders" => "Cancelled",
                _ => tabStatus
            };
        }

        // Status transitions live in Services/OrderWorkflow.cs — the single source of truth
        // shared with the KDS (KitchenService) and the POS. (Order-number generation lives in
        // PosService, where orders are created.)

        private async Task PopulateViewBagData(int? branchId)
        {
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.UserRole = HttpContext.Session.GetUserRole();
            ViewBag.UserName = HttpContext.Session.GetUserName();
            ViewBag.IsOwner = HttpContext.Session.IsOwner();
            ViewBag.IsBranchManager = HttpContext.Session.IsBranchManager();
            ViewBag.IsStaff = HttpContext.Session.IsStaff();
        }

        public async Task<IActionResult> ExportCsv(int? branchId, string? status, DateTime? from, DateTime? to)
        {
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .AsQueryable();

            if (!HttpContext.Session.IsOwner())
            {
                var accessibleBranches = await GetAccessibleBranches();
                var ids = accessibleBranches.Select(b => b.Id).ToList();
                query = query.Where(o => ids.Contains(o.BranchId));
            }
            else if (branchId.HasValue)
            {
                query = query.Where(o => o.BranchId == branchId.Value);
            }

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);
            if (from.HasValue)
                query = query.Where(o => o.OrderDate >= from.Value);
            if (to.HasValue)
                query = query.Where(o => o.OrderDate <= to.Value.AddDays(1));

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("OrderNumber,Date,Customer,Branch,Status,Amount");
            foreach (var o in orders)
            {
                csv.AppendLine($"{o.OrderNumber},{o.OrderDate:yyyy-MM-dd},{EscapeCsv(o.Customer?.Name ?? "")},{EscapeCsv(o.Branch?.Name ?? "")},{EscapeCsv(o.Status)},{o.TotalAmount:F2}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"orders-{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"\"")}\""; 
            return value;
        }
    }
}