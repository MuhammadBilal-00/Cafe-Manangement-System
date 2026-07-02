using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    /// <summary>
    /// Phase 10 helper: populates the current tenant with representative demo data for every module
    /// that ships empty (menu depth, supply chain, sales, accounting, marketing, HR, essentials,
    /// logistics) so each page shows realistic content. Idempotent — every block only seeds when its
    /// table is empty, so it is safe to run repeatedly. TenantId is stamped by the tenant interceptor.
    /// </summary>
    public interface IDemoDataService
    {
        Task<List<string>> SeedAsync(int? userId);
    }

    public class DemoDataService : IDemoDataService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAccountingService _accounting;
        private readonly ILogger<DemoDataService> _log;

        public DemoDataService(ApplicationDbContext db, IAccountingService accounting, ILogger<DemoDataService> log)
        {
            _db = db;
            _accounting = accounting;
            _log = log;
        }

        public async Task<List<string>> SeedAsync(int? userId)
        {
            var log = new List<string>();
            var branches = await _db.Branches.OrderBy(b => b.Id).Select(b => b.Id).ToListAsync();
            if (branches.Count == 0) { log.Add("No branches found — nothing to seed."); return log; }

            int b1 = branches[0];
            int b2 = branches.Count > 1 ? branches[1] : branches[0];
            var menu = await _db.MenuItems.OrderBy(m => m.Id).Select(m => m.Id).Take(8).ToListAsync();
            var inv = await _db.InventoryItems.OrderBy(i => i.Id).Select(i => i.Id).ToListAsync();
            var staff = await _db.Staff.OrderBy(s => s.Id).Select(s => s.Id).Take(6).ToListAsync();
            var custs = await _db.Customers.OrderBy(c => c.Id).Select(c => c.UserId).ToListAsync();
            var suppliers = await _db.Suppliers.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
            var orderIds = await _db.Orders.OrderByDescending(o => o.Id).Select(o => o.Id).Take(6).ToListAsync();
            var acct = await _db.Accounts.ToDictionaryAsync(a => a.Code, a => a.Id);

            async Task Block(string name, Func<Task> action)
            {
                try { await action(); await _db.SaveChangesAsync(); log.Add("✓ " + name); }
                catch (Exception ex) { log.Add("✗ " + name + ": " + ex.Message); _log.LogWarning(ex, "Demo seed block failed: {Block}", name); }
            }

            // ─────────────── Menu depth (Phase 2) ───────────────
            await Block("Brands", async () =>
            {
                if (await _db.Brands.AnyAsync()) return;
                _db.Brands.AddRange(
                    new Brand { Name = "House Roast", Description = "Signature in-house coffee blend" },
                    new Brand { Name = "Fresh Bakes", Description = "Daily baked goods" },
                    new Brand { Name = "Farm Dairy", Description = "Local dairy supplier brand" });
            });

            await Block("Units", async () =>
            {
                if (await _db.Units.AnyAsync()) return;
                _db.Units.AddRange(
                    new Unit { Name = "Piece", Abbreviation = "pc", ConversionFactor = 1 },
                    new Unit { Name = "Kilogram", Abbreviation = "kg", ConversionFactor = 1 },
                    new Unit { Name = "Gram", Abbreviation = "g", ConversionFactor = 0.001m },
                    new Unit { Name = "Litre", Abbreviation = "L", ConversionFactor = 1 });
            });

            await Block("Modifier groups & options", async () =>
            {
                if (await _db.ModifierGroups.AnyAsync()) return;
                var size = new ModifierGroup { Name = "Size", MinSelect = 1, MaxSelect = 1, IsRequired = true, SortOrder = 1 };
                var addons = new ModifierGroup { Name = "Add-ons", MinSelect = 0, MaxSelect = 3, IsRequired = false, SortOrder = 2 };
                _db.ModifierGroups.AddRange(size, addons);
                await _db.SaveChangesAsync();
                _db.Modifiers.AddRange(
                    new Modifier { ModifierGroupId = size.Id, Name = "Small", PriceDelta = -30, SortOrder = 1 },
                    new Modifier { ModifierGroupId = size.Id, Name = "Regular", PriceDelta = 0, SortOrder = 2 },
                    new Modifier { ModifierGroupId = size.Id, Name = "Large", PriceDelta = 60, SortOrder = 3 },
                    new Modifier { ModifierGroupId = addons.Id, Name = "Extra Shot", PriceDelta = 80 },
                    new Modifier { ModifierGroupId = addons.Id, Name = "Whipped Cream", PriceDelta = 50 },
                    new Modifier { ModifierGroupId = addons.Id, Name = "Caramel Drizzle", PriceDelta = 40 });
                await _db.SaveChangesAsync();
                // Attach both groups to the first few menu items so the register shows modifiers.
                if (!await _db.MenuItemModifierGroups.AnyAsync())
                    foreach (var mid in menu.Take(4))
                        _db.MenuItemModifierGroups.AddRange(
                            new MenuItemModifierGroup { MenuItemId = mid, ModifierGroupId = size.Id },
                            new MenuItemModifierGroup { MenuItemId = mid, ModifierGroupId = addons.Id });
            });

            await Block("Price groups & overrides", async () =>
            {
                if (await _db.PriceGroups.AnyAsync()) return;
                var retail = new PriceGroup { Name = "Standard", Description = "Walk-in retail pricing" };
                var wholesale = new PriceGroup { Name = "Corporate", Description = "Bulk / corporate accounts" };
                _db.PriceGroups.AddRange(retail, wholesale);
                await _db.SaveChangesAsync();
                foreach (var mid in menu.Take(5))
                    _db.MenuItemPrices.Add(new MenuItemPrice { MenuItemId = mid, PriceGroupId = wholesale.Id, Price = 180 });
            });

            await Block("Combos", async () =>
            {
                if (await _db.Combos.AnyAsync() || menu.Count < 2) return;
                var combo = new Combo { BranchId = b1, Name = "Coffee + Pastry Deal", Description = "Any coffee with a pastry", Price = 350 };
                _db.Combos.Add(combo);
                await _db.SaveChangesAsync();
                _db.ComboItems.AddRange(
                    new ComboItem { ComboId = combo.Id, MenuItemId = menu[0], Quantity = 1 },
                    new ComboItem { ComboId = combo.Id, MenuItemId = menu[1], Quantity = 1 });
            });

            // ─────────────── Restaurant tables (Phase 1) ───────────────
            await Block("Restaurant tables", async () =>
            {
                if (await _db.RestaurantTables.AnyAsync()) return;
                foreach (var z in new[] { "Main", "Patio", "Rooftop" })
                    for (int n = 1; n <= 4; n++)
                        _db.RestaurantTables.Add(new RestaurantTable { BranchId = b1, Name = $"{z[0]}{n}", Zone = z, Capacity = n % 2 == 0 ? 4 : 2, Status = "Available" });
            });

            // ─────────────── Tax groups (Phase 4) ───────────────
            await Block("Tax groups", async () =>
            {
                if (await _db.TaxGroups.AnyAsync()) return;
                var gst = new TaxGroup { Name = "Standard GST" };
                var compound = new TaxGroup { Name = "GST + Service" };
                _db.TaxGroups.AddRange(gst, compound);
                await _db.SaveChangesAsync();
                _db.Taxes.AddRange(
                    new Tax { TaxGroupId = gst.Id, Name = "GST", Rate = 17, SortOrder = 1 },
                    new Tax { TaxGroupId = compound.Id, Name = "GST", Rate = 17, SortOrder = 1 },
                    new Tax { TaxGroupId = compound.Id, Name = "Service Charge", Rate = 5, IsCompound = true, SortOrder = 2 });
            });

            // ─────────────── Supply chain (Phase 3) ───────────────
            await Block("Stock transfer", async () =>
            {
                if (await _db.StockTransfers.AnyAsync() || inv.Count == 0) return;
                var tr = new StockTransfer { FromBranchId = b1, ToBranchId = b2, Status = "Completed", Reference = "TRF-DEMO-1", Notes = "Demo inter-branch transfer", CreatedById = userId, CompletedAt = DateTime.Now };
                _db.StockTransfers.Add(tr);
                await _db.SaveChangesAsync();
                _db.StockTransferItems.Add(new StockTransferItem { StockTransferId = tr.Id, InventoryItemId = inv[0], Quantity = 5 });
            });

            await Block("Stock adjustment", async () =>
            {
                if (await _db.StockAdjustments.AnyAsync() || inv.Count == 0) return;
                var adj = new StockAdjustment { BranchId = b1, Type = "Decrease", Reason = "Wastage — spoiled stock", ApprovalStatus = "Approved", CreatedById = userId, ApprovedById = userId, ApprovedAt = DateTime.Now };
                _db.StockAdjustments.Add(adj);
                await _db.SaveChangesAsync();
                _db.StockAdjustmentLines.Add(new StockAdjustmentLine { StockAdjustmentId = adj.Id, InventoryItemId = inv[0], QuantityDelta = -2 });
            });

            await Block("Production order", async () =>
            {
                if (await _db.ProductionOrders.AnyAsync() || inv.Count < 2) return;
                var po = new ProductionOrder { BranchId = b1, OutputInventoryItemId = inv[0], OutputQuantity = 10, Status = "Draft", Notes = "Demo batch", CreatedById = userId };
                _db.ProductionOrders.Add(po);
                await _db.SaveChangesAsync();
                _db.ProductionInputs.Add(new ProductionInput { ProductionOrderId = po.Id, InventoryItemId = inv[1], Quantity = 3 });
            });

            // ─────────────── Sales / returns / receivables (Phase 4) ───────────────
            await Block("Quotation", async () =>
            {
                if (await _db.Quotations.AnyAsync() || menu.Count == 0) return;
                var q = new Quotation { BranchId = b1, CustomerId = custs.FirstOrDefault(), QuotationNumber = "QT-DEMO-1", Status = "Sent", Subtotal = 500, Notes = "Corporate catering quote", CreatedById = userId };
                _db.Quotations.Add(q);
                await _db.SaveChangesAsync();
                _db.QuotationItems.Add(new QuotationItem { QuotationId = q.Id, MenuItemId = menu[0], Quantity = 2, Price = 250 });
            });

            await Block("Sell return", async () =>
            {
                if (await _db.SellReturns.AnyAsync() || inv.Count == 0) return;
                var sr = new SellReturn { BranchId = b1, CustomerId = custs.FirstOrDefault(), ReturnNumber = "SR-DEMO-1", Status = "Approved", TotalAmount = 250, Reason = "Wrong item served", CreatedById = userId, ApprovedById = userId, ApprovedAt = DateTime.Now };
                _db.SellReturns.Add(sr);
                await _db.SaveChangesAsync();
                _db.SellReturnLines.Add(new SellReturnLine { SellReturnId = sr.Id, InventoryItemId = inv[0], Quantity = 1, UnitValue = 250 });
            });

            await Block("Purchase return", async () =>
            {
                if (await _db.PurchaseReturns.AnyAsync() || inv.Count == 0) return;
                var pr = new PurchaseReturn { BranchId = b1, SupplierId = suppliers.FirstOrDefault(), ReturnNumber = "PR-DEMO-1", Status = "Approved", TotalAmount = 300, Reason = "Damaged on delivery", CreatedById = userId, ApprovedById = userId, ApprovedAt = DateTime.Now };
                _db.PurchaseReturns.Add(pr);
                await _db.SaveChangesAsync();
                _db.PurchaseReturnLines.Add(new PurchaseReturnLine { PurchaseReturnId = pr.Id, InventoryItemId = inv[0], Quantity = 2, UnitCost = 150 });
            });

            await Block("Supplier payments", async () =>
            {
                if (await _db.SupplierPayments.AnyAsync() || suppliers.Count == 0) return;
                _db.SupplierPayments.AddRange(
                    new SupplierPayment { SupplierId = suppliers[0], BranchId = b1, Amount = 5000, Method = "BankTransfer", Reference = "PAY-DEMO-1", CreatedById = userId },
                    new SupplierPayment { SupplierId = suppliers[0], BranchId = b1, Amount = 2500, Method = "Cash", Reference = "PAY-DEMO-2", CreatedById = userId });
            });

            // ─────────────── Accounting (Phase 5) ───────────────
            await Block("Payment accounts", async () =>
            {
                if (await _db.PaymentAccounts.AnyAsync()) return;
                _db.PaymentAccounts.AddRange(
                    new PaymentAccount { Name = "Main Cash Drawer", Type = "Cash", AccountId = acct.GetValueOrDefault("1000"), OpeningBalance = 20000 },
                    new PaymentAccount { Name = "Business Bank — Meezan", Type = "Bank", AccountId = acct.GetValueOrDefault("1010"), OpeningBalance = 150000 });
            });

            await Block("Budget & lines", async () =>
            {
                if (await _db.Budgets.AnyAsync()) return;
                var bud = new Budget { Name = $"Operating Budget {DateTime.Now.Year}", Year = DateTime.Now.Year, BranchId = b1 };
                _db.Budgets.Add(bud);
                await _db.SaveChangesAsync();
                if (acct.TryGetValue("6000", out var opex)) _db.BudgetLines.Add(new BudgetLine { BudgetId = bud.Id, AccountId = opex, Amount = 120000 });
                if (acct.TryGetValue("6100", out var pay)) _db.BudgetLines.Add(new BudgetLine { BudgetId = bud.Id, AccountId = pay, Amount = 300000 });
            });

            await Block("Auto-post journals (invoices/expenses/purchases)", async () =>
            {
                if (await _db.JournalEntries.AnyAsync()) return;
                var n = await _accounting.AutoPostAsync(userId);
                log.Add($"   → auto-posted {n} journal entries");
            });

            // ─────────────── Marketing / portal (Phase 6) ───────────────
            await Block("Loyalty points & ledger", async () =>
            {
                if (await _db.LoyaltyTransactions.AnyAsync() || custs.Count == 0) return;
                var cs = await _db.Customers.Where(c => custs.Contains(c.UserId)).Take(4).ToListAsync();
                foreach (var c in cs)
                {
                    c.LoyaltyPoints = 120;
                    _db.LoyaltyTransactions.Add(new LoyaltyTransaction { CustomerUserId = c.UserId, Points = 120, Type = "Earn", Note = "Welcome bonus" });
                }
            });

            await Block("Gift cards", async () =>
            {
                if (await _db.GiftCards.AnyAsync()) return;
                var gc = new GiftCard { Code = "GC-DEMO-1000", InitialBalance = 1000, Balance = 1000, CustomerUserId = custs.FirstOrDefault(), IsActive = true };
                _db.GiftCards.Add(gc);
                await _db.SaveChangesAsync();
                _db.GiftCardTransactions.Add(new GiftCardTransaction { GiftCardId = gc.Id, Amount = 1000, Note = "Issued" });
                _db.GiftCards.Add(new GiftCard { Code = "GC-DEMO-500", InitialBalance = 500, Balance = 500, IsActive = true });
            });

            await Block("Notification templates", async () =>
            {
                if (await _db.NotificationTemplates.AnyAsync()) return;
                _db.NotificationTemplates.AddRange(
                    new NotificationTemplate { Key = "order.ready", Name = "Order Ready", Channel = "SMS", Body = "Hi {name}, your order #{order} is ready for pickup!" },
                    new NotificationTemplate { Key = "welcome", Name = "Welcome Email", Channel = "Email", Subject = "Welcome to {business}", Body = "Thanks for joining our rewards program, {name}!" });
            });

            await Block("CRM leads, follow-ups & campaign", async () =>
            {
                if (await _db.Leads.AnyAsync()) return;
                var l1 = new Lead { Name = "Ayesha Traders", Email = "ayesha@corp.pk", Phone = "0300-1234567", Source = "Website", Status = "New", Notes = "Interested in office catering" };
                var l2 = new Lead { Name = "TechHub Offices", Email = "admin@techhub.pk", Phone = "0321-9876543", Source = "Referral", Status = "Contacted" };
                _db.Leads.AddRange(l1, l2);
                await _db.SaveChangesAsync();
                _db.FollowUps.Add(new FollowUp { LeadId = l1.Id, DueAt = DateTime.Now.AddDays(2), Note = "Send catering menu & pricing" });
                if (!await _db.Campaigns.AnyAsync())
                    _db.Campaigns.Add(new Campaign { Name = "Ramadan Special", Channel = "Email", Segment = "AllCustomers", Subject = "Iftar deals just for you", Body = "Enjoy 20% off all platters this Ramadan.", Status = "Draft" });
            });

            // ─────────────── HR depth (Phase 7) ───────────────
            await Block("Departments & designations", async () =>
            {
                if (!await _db.Departments.AnyAsync())
                    _db.Departments.AddRange(
                        new Department { Name = "Kitchen" }, new Department { Name = "Front of House" },
                        new Department { Name = "Management" }, new Department { Name = "Delivery" });
                if (!await _db.Designations.AnyAsync())
                    _db.Designations.AddRange(
                        new Designation { Name = "Barista" }, new Designation { Name = "Chef" },
                        new Designation { Name = "Cashier" }, new Designation { Name = "Branch Manager" });
            });

            await Block("Leave types", async () =>
            {
                if (await _db.LeaveTypes.AnyAsync()) return;
                _db.LeaveTypes.AddRange(
                    new LeaveType { Name = "Annual Leave", DaysPerYear = 20, IsPaid = true, AttendanceStatus = "Paid Leave" },
                    new LeaveType { Name = "Sick Leave", DaysPerYear = 10, IsPaid = true, AttendanceStatus = "Sick Leave" },
                    new LeaveType { Name = "Casual Leave", DaysPerYear = 8, IsPaid = true, AttendanceStatus = "Casual Leave" });
            });

            await Block("Leave request", async () =>
            {
                if (await _db.LeaveRequests.AnyAsync() || staff.Count == 0) return;
                var lt = await _db.LeaveTypes.OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync();
                if (lt == 0) return;
                _db.LeaveRequests.Add(new LeaveRequest { StaffId = staff[0], LeaveTypeId = lt, FromDate = DateTime.Today.AddDays(7), ToDate = DateTime.Today.AddDays(9), Days = 3, Reason = "Family event", Status = "Pending" });
            });

            await Block("Holidays", async () =>
            {
                if (await _db.Holidays.AnyAsync()) return;
                var yr = DateTime.Now.Year;
                _db.Holidays.AddRange(
                    new Holiday { Name = "Independence Day", Date = new DateTime(yr, 8, 14), IsRecurring = true },
                    new Holiday { Name = "Labour Day", Date = new DateTime(yr, 5, 1), IsRecurring = true });
            });

            await Block("Sales targets", async () =>
            {
                if (await _db.SalesTargets.AnyAsync() || staff.Count == 0) return;
                _db.SalesTargets.Add(new SalesTarget { StaffId = staff[0], BranchId = b1, Year = DateTime.Now.Year, Month = DateTime.Now.Month, TargetAmount = 200000, CommissionPercent = 2 });
            });

            await Block("Employee documents", async () =>
            {
                if (await _db.EmployeeDocuments.AnyAsync() || staff.Count == 0) return;
                _db.EmployeeDocuments.Add(new EmployeeDocument { StaffId = staff[0], Title = "Employment Contract", DocType = "Contract", FileUrl = "/uploads/employee/sample-contract.pdf", ExpiresAt = DateTime.Today.AddYears(1) });
            });

            // ─────────────── Essentials (Phase 8) ───────────────
            await Block("Documents, memos, reminders, KB, messages", async () =>
            {
                if (!await _db.Documents.AnyAsync())
                    _db.Documents.Add(new Document { Title = "Health & Safety Policy", Category = "Compliance", FileUrl = "/uploads/documents/hs-policy.pdf", Notes = "Reviewed annually", CreatedById = userId });
                if (!await _db.Memos.AnyAsync())
                    _db.Memos.Add(new Memo { Title = "Weekend rush prep", Body = "Extra staff scheduled Fri–Sun. Prep stock a day ahead.", Pinned = true, CreatedById = userId });
                if (!await _db.Reminders.AnyAsync())
                    _db.Reminders.Add(new Reminder { Title = "Renew food licence", DueAt = DateTime.Now.AddDays(30), OwnerId = userId });
                if (!await _db.KnowledgeBaseArticles.AnyAsync())
                    _db.KnowledgeBaseArticles.Add(new KnowledgeBaseArticle { Title = "How to close the register", Category = "Operations" });
                if (!await _db.Messages.AnyAsync() && userId.HasValue)
                {
                    var other = await _db.Users.Where(u => u.Id != userId.Value).Select(u => u.Id).FirstOrDefaultAsync();
                    if (other != 0) _db.Messages.Add(new Message { FromUserId = userId.Value, ToUserId = other, Body = "Welcome to the team messaging channel!" });
                }
            });

            // ─────────────── Logistics (Phase 9) ───────────────
            await Block("Riders", async () =>
            {
                if (await _db.Riders.AnyAsync()) return;
                _db.Riders.AddRange(
                    new Rider { Name = "Imran Ali", Phone = "0300-1112223", Vehicle = "Bike", BranchId = b1 },
                    new Rider { Name = "Bilal Khan", Phone = "0333-4445556", Vehicle = "Scooter", BranchId = b1 });
            });

            await Block("Deliveries", async () =>
            {
                if (await _db.Deliveries.AnyAsync() || orderIds.Count == 0) return;
                var rider = await _db.Riders.OrderBy(r => r.Id).Select(r => r.Id).FirstOrDefaultAsync();
                _db.Deliveries.Add(new Delivery { OrderId = orderIds[0], RiderId = rider == 0 ? null : rider, Status = "Assigned", Fee = 120, Address = "12-C Gulberg III, Lahore", AssignedAt = DateTime.Now });
                if (orderIds.Count > 1)
                    _db.Deliveries.Add(new Delivery { OrderId = orderIds[1], Status = "Pending", Fee = 150, Address = "House 5, DHA Phase 5" });
            });

            await Block("Shipments", async () =>
            {
                if (await _db.Shipments.AnyAsync()) return;
                _db.Shipments.Add(new Shipment { OrderId = orderIds.FirstOrDefault() == 0 ? null : orderIds.FirstOrDefault(), Carrier = "TCS", TrackingNumber = "TCS-DEMO-77421", Status = "Shipped", ShippedAt = DateTime.Now });
            });

            await Block("POS / receipt profiles", async () =>
            {
                if (await _db.PosProfiles.AnyAsync()) return;
                _db.PosProfiles.AddRange(
                    new PosProfile { Name = "Counter A5", PaperSize = "A5", ShowLogo = true, ShowTaxBreakdown = true, IsDefault = true, BranchId = b1 },
                    new PosProfile { Name = "Thermal 80mm", PaperSize = "Thermal80", ShowLogo = false, ShowTaxBreakdown = true, BranchId = b1 });
            });

            // ─────────────── Recipes: link menu items → raw stock so orders consume inventory ───────────────
            await Block("Recipe mappings (menu → inventory)", async () =>
            {
                if (await _db.InventoryRecipeMappings.AnyAsync()) return;
                // Map each menu item to a raw inventory item IN THE SAME BRANCH, so ordering it deducts stock
                // (the deduction path only consumes branch-local inventory).
                var menuItems = await _db.MenuItems.Select(m => new { m.Id, m.BranchId }).ToListAsync();
                var invByBranch = (await _db.InventoryItems.Select(i => new { i.Id, i.BranchId }).ToListAsync())
                    .GroupBy(i => i.BranchId).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
                foreach (var m in menuItems)
                    if (invByBranch.TryGetValue(m.BranchId, out var list) && list.Count > 0)
                        _db.InventoryRecipeMappings.Add(new InventoryRecipeMapping
                        {
                            MenuItemId = m.Id, InventoryItemId = list[m.Id % list.Count], QuantityRequired = 1, Unit = "pc"
                        });
            });

            // ─────────────── Kitchen printers + station routing (KOT) ───────────────
            await Block("Kitchen printers", async () =>
            {
                if (await _db.KitchenPrinters.AnyAsync()) return;
                // Browser printers render a visible KOT (works without hardware). Network is also supported
                // via the admin screen (IP:9100 ESC/POS). Default catches unrouted categories.
                _db.KitchenPrinters.AddRange(
                    new KitchenPrinter { BranchId = b1, Name = "Main Kitchen", ConnectionType = "Browser", Station = "Kitchen", IsDefault = true, IsActive = true },
                    new KitchenPrinter { BranchId = b1, Name = "Bar", ConnectionType = "Browser", Station = "Bar", IsActive = true });
            });

            await Block("Category → station routing", async () =>
            {
                // Route drink categories to the Bar; everything else falls to the default (Kitchen) printer.
                var drinkWords = new[] { "beverage", "drink", "coffee", "tea", "juice", "shake", "smoothie", "bar" };
                var cats = await _db.Categories.ToListAsync();
                var changed = false;
                foreach (var c in cats.Where(c => string.IsNullOrWhiteSpace(c.KotStation)))
                {
                    if (drinkWords.Any(w => c.Name.ToLower().Contains(w))) { c.KotStation = "Bar"; changed = true; }
                }
                if (!changed) log.Add("   → (no drink categories matched; items route to default Kitchen printer)");
            });

            log.Add($"Done. {log.Count(l => l.StartsWith("✓"))} module blocks seeded.");
            return log;
        }
    }
}
