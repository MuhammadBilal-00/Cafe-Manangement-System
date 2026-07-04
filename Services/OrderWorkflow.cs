using Cafe.Models;

namespace Cafe.Services
{
    /// <summary>
    /// SINGLE source of truth for order/kitchen status transitions and for keeping the two
    /// fields in agreement. Order Management, the KDS and the POS all route through here so
    /// the boards can never diverge:
    ///
    ///   POS finalize (KOT fired)      → Order = Preparing, Kitchen = New
    ///   KDS New → Cooking             → Order stays Preparing
    ///   KDS → Ready                   → Order = Ready
    ///   KDS → Served / OM "Done"      → Order = Completed, Kitchen = Served
    ///   OM Cancel                     → Kitchen ticket closed (leaves every live board)
    ///
    /// "Pending" is reserved for orders NOT yet fired to the kitchen (held/suspended/drafts).
    /// All transitions are forward-only; syncs that would move a field backward are no-ops.
    /// </summary>
    public static class OrderWorkflow
    {
        // Forward-only order workflow: Pending -> Preparing -> Ready -> Completed.
        // Cancellation is allowed from any non-terminal state; no backward/skip-ahead moves.
        public static readonly Dictionary<string, string[]> OrderTransitions = new()
        {
            ["Pending"]   = new[] { "Preparing", "Cancelled" },
            ["Preparing"] = new[] { "Ready", "Cancelled" },
            ["Ready"]     = new[] { "Completed", "Cancelled" },
            ["Completed"] = Array.Empty<string>(),
            ["Cancelled"] = Array.Empty<string>()
        };

        // Forward-only KDS workflow: New -> Cooking -> Ready -> Served.
        public static readonly Dictionary<string, string[]> KitchenTransitions = new()
        {
            ["New"]     = new[] { "Cooking", "Ready", "Served" },
            ["Cooking"] = new[] { "Ready", "Served" },
            ["Ready"]   = new[] { "Served" },
            ["Served"]  = Array.Empty<string>()
        };

        public static bool CanTransitionOrder(string from, string to) =>
            OrderTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

        public static bool CanTransitionKitchen(string from, string to) =>
            KitchenTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

        /// <summary>
        /// After a KDS transition, pull the order status forward to match the kitchen:
        /// New/Cooking → Preparing, Ready → Ready, Served → Completed. Never moves backward.
        /// </summary>
        public static void SyncOrderFromKitchen(Order order)
        {
            var target = order.KitchenStatus switch
            {
                "New" or "Cooking" => "Preparing",
                "Ready" => "Ready",
                "Served" => "Completed",
                _ => null
            };
            if (target != null && order.Status != target && CanTransitionOrder(order.Status, target))
                order.Status = target;
        }

        /// <summary>
        /// After an Order Management transition, pull the kitchen ticket forward to match:
        /// Ready → Ready, Completed → Served. Cancelled also closes the ticket (Served) so it
        /// leaves every live kitchen board — the kitchen has nothing left to act on. Preparing
        /// deliberately leaves the ticket alone (New and Cooking both mean "being prepared").
        /// </summary>
        public static void SyncKitchenFromOrder(Order order)
        {
            var target = order.Status switch
            {
                "Ready" => "Ready",
                "Completed" or "Cancelled" => "Served",
                _ => null
            };
            if (target != null && order.KitchenStatus != target && CanTransitionKitchen(order.KitchenStatus, target))
                order.KitchenStatus = target;
        }
    }
}
