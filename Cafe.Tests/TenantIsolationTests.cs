using Cafe.Data;
using Cafe.Interceptors;
using Cafe.Models;
using Cafe.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafe.Tests
{
    /// <summary>
    /// IDOR / tenant-isolation tests. These exercise the REAL <see cref="ApplicationDbContext"/>
    /// (global query filters + the stamping interceptor) against a throwaway SQL Server database,
    /// so they faithfully reproduce production behaviour. Required before Phase 1.
    /// </summary>
    public class TenantIsolationTests : IClassFixture<TenantIsolationTests.DbFixture>
    {
        private readonly DbFixture _fx;
        public TenantIsolationTests(DbFixture fx) => _fx = fx;

        private ApplicationDbContext ContextFor(int? tenantId, bool platform = false)
        {
            var tc = new TenantContext();
            tc.SetTenant(tenantId, platform);
            return _fx.NewContext(tc);
        }

        [Fact]
        public void TenantA_sees_only_its_own_orders()
        {
            using var ctx = ContextFor(_fx.TenantAId);
            var orders = ctx.Orders.ToList();
            Assert.Single(orders);
            Assert.All(orders, o => Assert.Equal(_fx.TenantAId, o.TenantId));
        }

        [Fact]
        public void TenantB_sees_only_its_own_orders()
        {
            using var ctx = ContextFor(_fx.TenantBId);
            var orders = ctx.Orders.ToList();
            Assert.Single(orders);
            Assert.All(orders, o => Assert.Equal(_fx.TenantBId, o.TenantId));
        }

        [Fact]
        public void TenantA_cannot_read_tenantB_order_by_id()
        {
            using var ctx = ContextFor(_fx.TenantAId);
            var foreign = ctx.Orders.FirstOrDefault(o => o.Id == _fx.TenantBOrderId);
            Assert.Null(foreign); // IDOR by id is blocked by the global filter
        }

        [Fact]
        public void TenantA_cannot_update_tenantB_order()
        {
            using var ctx = ContextFor(_fx.TenantAId);
            var foreign = ctx.Orders.FirstOrDefault(o => o.Id == _fx.TenantBOrderId);
            Assert.Null(foreign); // can't even load it, so it can't be tampered with
        }

        [Fact]
        public void TenantA_cannot_see_tenantB_users()
        {
            using var ctx = ContextFor(_fx.TenantAId);
            var users = ctx.Users.ToList();
            Assert.All(users, u => Assert.Equal(_fx.TenantAId, u.TenantId));
        }

        [Fact]
        public void Insert_is_stamped_with_current_tenant()
        {
            int newId;
            using (var ctx = ContextFor(_fx.TenantAId))
            {
                var branch = new Branch { Name = "Stamp Test", Location = "x", ContactInfo = "x" };
                ctx.Branches.Add(branch);     // TenantId intentionally left unset (0)
                ctx.SaveChanges();
                newId = branch.Id;
                Assert.Equal(_fx.TenantAId, branch.TenantId); // interceptor stamped it
            }

            // And tenant B cannot see it.
            using var bctx = ContextFor(_fx.TenantBId);
            Assert.Null(bctx.Branches.FirstOrDefault(b => b.Id == newId));
        }

        [Fact]
        public void PlatformAdmin_bypasses_the_filter_and_sees_all()
        {
            using var ctx = ContextFor(null, platform: true);
            var orders = ctx.Orders.ToList();
            Assert.True(orders.Count >= 2, $"platform should see both tenants' orders, saw {orders.Count}");
        }

        // ── Fixture: builds a throwaway DB and seeds two tenants ──
        public class DbFixture : IDisposable
        {
            private const string ConnString =
                "Server=BILAL\\SQLEXPRESS;Database=RestaurantManagementDB_Phase0Tests;Trusted_Connection=true;TrustServerCertificate=true;MultipleActiveResultSets=true;";

            public int TenantAId { get; }
            public int TenantBId { get; }
            public int TenantAOrderId { get; }
            public int TenantBOrderId { get; }

            public ApplicationDbContext NewContext(ITenantContext tc)
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(ConnString)
                    .AddInterceptors(new TenantStampingInterceptor(tc))
                    .Options;
                return new ApplicationDbContext(options, tc);
            }

            public DbFixture()
            {
                // Fresh schema (no migrations needed for the test DB).
                var bypass = new TenantContext(); // default: filter ignored
                using (var setup = NewContext(bypass))
                {
                    setup.Database.EnsureDeleted();
                    // Apply the real migrations (not EnsureCreated) so the schema matches production
                    // exactly, including the Phase 0 retrofit migration (a no-op on this empty DB).
                    setup.Database.Migrate();

                    var tenantA = new Tenant { Name = "Tenant A", Slug = "tenant-a", Status = "Active" };
                    var tenantB = new Tenant { Name = "Tenant B", Slug = "tenant-b", Status = "Active" };
                    setup.Tenants.AddRange(tenantA, tenantB);
                    setup.SaveChanges();
                    TenantAId = tenantA.Id;
                    TenantBId = tenantB.Id;

                    var (ua, ba, oa) = Seed(setup, tenantA.Id, "a");
                    var (ub, bb, ob) = Seed(setup, tenantB.Id, "b");
                    setup.SaveChanges();
                    TenantAOrderId = oa.Id;
                    TenantBOrderId = ob.Id;
                }
            }

            private static (User, Branch, Order) Seed(ApplicationDbContext ctx, int tenantId, string tag)
            {
                // TenantId set explicitly because the seeding context bypasses stamping.
                var user = new User { Name = $"Cust {tag}", Email = $"cust-{tag}@t.local", Phone = "0", Role = "Customer", TenantId = tenantId };
                ctx.Users.Add(user);
                var branch = new Branch { Name = $"Branch {tag}", Location = "x", ContactInfo = "x", TenantId = tenantId };
                ctx.Branches.Add(branch);
                ctx.SaveChanges();

                var order = new Order
                {
                    OrderNumber = $"ORD-{tag}-1",
                    CustomerId = user.Id,
                    BranchId = branch.Id,
                    Status = "Pending",
                    TotalAmount = 100m,
                    TenantId = tenantId
                };
                ctx.Orders.Add(order);
                ctx.SaveChanges();
                return (user, branch, order);
            }

            public void Dispose()
            {
                var bypass = new TenantContext();
                using var ctx = NewContext(bypass);
                ctx.Database.EnsureDeleted();
            }
        }
    }
}
