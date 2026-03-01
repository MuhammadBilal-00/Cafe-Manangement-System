using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        private readonly IInventoryService _inventoryService;
        private readonly INotificationService _notificationService;

        public OrderController(ApplicationDbContext context, IInventoryService inventoryService, INotificationService notificationService) : base(context)
        {
            _inventoryService = inventoryService;
            _notificationService = notificationService;
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
                    customerName = o.Customer.Name,
                    customerPhone = o.Customer.Phone,
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
                    name = order.Customer.Name,
                    email = order.Customer.Email,
                    phone = order.Customer.Phone
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
                subtotal = order.OrderItems.Sum(oi => oi.Quantity * oi.Price),
                tax = order.OrderItems.Sum(oi => oi.Quantity * oi.Price) * 0.08m,
                serviceCharge = 1.50m,
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

        // Create New Order
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                if (!CanAccessBranch(request.BranchId))
                {
                    return Json(new { success = false, message = "Access denied to this branch" });
                }

                // Check inventory availability for all items before creating order
                foreach (var item in request.Items)
                {
                    var hasInventory = await _inventoryService.CheckInventoryAvailability(
                        item.MenuItemId, 
                        item.Quantity, 
                        request.BranchId
                    );

                    if (!hasInventory)
                    {
                        var menuItem = await _context.MenuItems.FindAsync(item.MenuItemId);
                        return Json(new { 
                            success = false, 
                            message = $"Insufficient inventory for {menuItem?.Name}. Please check stock levels." 
                        });
                    }
                }

                var order = new Order
                {
                    OrderNumber = await GenerateOrderNumber(request.BranchId),
                    CustomerId = request.CustomerId,
                    BranchId = request.BranchId,
                    OrderDate = DateTime.Now,
                    Status = "Pending",
                    Notes = request.Notes,
                    TotalAmount = 0
                };

                // Calculate total and create order items
                decimal total = 0;
                var orderItems = new List<OrderItem>();

                foreach (var item in request.Items)
                {
                    var menuItem = await _context.MenuItems.FindAsync(item.MenuItemId);
                    if (menuItem != null && menuItem.Availability && menuItem.BranchId == request.BranchId)
                    {
                        var orderItem = new OrderItem
                        {
                            MenuItemId = item.MenuItemId,
                            Quantity = item.Quantity,
                            Price = menuItem.Price
                        };
                        orderItems.Add(orderItem);
                        total += orderItem.Price * orderItem.Quantity;
                    }
                }

                if (!orderItems.Any())
                {
                    return Json(new { success = false, message = "No valid items found" });
                }

                order.TotalAmount = total;
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Add order items
                foreach (var item in orderItems)
                {
                    item.OrderId = order.Id;
                    _context.OrderItems.Add(item);
                }

                await _context.SaveChangesAsync();

                // Deduct inventory
                var userName = HttpContext.Session.GetUserName() ?? "System";
                var inventoryDeducted = await _inventoryService.DeductInventoryForOrder(
                    order.Id, 
                    request.BranchId, 
                    userName
                );

                if (!inventoryDeducted)
                {
                    // If inventory deduction fails, we still keep the order but log a warning
                    order.Notes = string.IsNullOrEmpty(order.Notes) 
                        ? "Warning: Inventory deduction failed" 
                        : $"{order.Notes}\nWarning: Inventory deduction failed";
                    await _context.SaveChangesAsync();
                }

                // Notification: new order created
                await _notificationService.CreateNotificationAsync(
                    "New Order Created",
                    $"Order #{order.OrderNumber} for {order.TotalAmount:C} has been placed.",
                    "Success", NotificationCategory.Order,
                    branchId: request.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: $"/Order/Index",
                    icon: "fas fa-receipt");

                return Json(new { success = true, orderId = order.Id, orderNumber = order.OrderNumber });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating order: " + ex.Message });
            }
        }

        // Update Order Status
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateStatusRequest request)
        {
            try
            {
                var order = await _context.Orders.FindAsync(request.OrderId);
                if (order == null || !CanAccessBranch(order.BranchId))
                {
                    return Json(new { success = false, message = "Order not found or access denied" });
                }

                // Validate status transition
                if (!IsValidStatusTransition(order.Status, request.NewStatus))
                {
                    return Json(new { success = false, message = "Invalid status transition" });
                }

                order.Status = request.NewStatus;
                await _context.SaveChangesAsync();

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
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
            }
        }

        // Get Menu Items for Branch (for Create Order modal)
        [HttpGet]
        public async Task<IActionResult> GetMenuItems(int branchId)
        {
            if (!CanAccessBranch(branchId))
            {
                return Forbid();
            }

            var menuItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.BranchId == branchId && m.Availability)
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    price = m.Price,
                    category = m.Category.Name,
                    description = m.Description
                })
                .OrderBy(m => m.category)
                .ThenBy(m => m.name)
                .ToListAsync();

            return Json(menuItems);
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
                    PasswordHash = "",
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
                return Json(new { success = false, message = "Error creating customer: " + ex.Message });
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
                order.Notes = string.IsNullOrEmpty(order.Notes)
                    ? $"Cancelled: {request.Reason}"
                    : $"{order.Notes}\nCancelled: {request.Reason}";

                await _context.SaveChangesAsync();

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
                return Json(new { success = false, message = "Error cancelling order: " + ex.Message });
            }
        }

        // Generate Receipt
        [HttpGet]
        public async Task<IActionResult> GenerateReceipt(int id)
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

            return View("Receipt", order);
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

        private bool IsValidStatusTransition(string currentStatus, string newStatus)
        {
            // Don't allow changes from completed or cancelled
            if (currentStatus == "Completed" || currentStatus == "Cancelled")
                return false;

            // Valid statuses
            var validStatuses = new[] { "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
            return validStatuses.Contains(newStatus);
        }

        private async Task<string> GenerateOrderNumber(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            var branchCode = branch?.Name.Substring(0, Math.Min(3, branch.Name.Length)).ToUpper() ?? "ORD";
            var today = DateTime.Now.ToString("yyyyMMdd");

            var lastOrder = await _context.Orders
                .Where(o => o.OrderNumber.StartsWith($"{branchCode}{today}"))
                .OrderByDescending(o => o.OrderNumber)
                .FirstOrDefaultAsync();

            int sequence = 1;
            if (lastOrder != null)
            {
                var lastSequence = lastOrder.OrderNumber.Substring($"{branchCode}{today}".Length);
                if (int.TryParse(lastSequence, out int lastSeq))
                {
                    sequence = lastSeq + 1;
                }
            }

            return $"{branchCode}{today}{sequence:D3}";
        }

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