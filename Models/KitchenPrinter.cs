using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// A kitchen/bar printer that receives Kitchen Order Tickets (KOTs). Two connection types:
    ///  • Network — the server sends raw ESC/POS bytes over TCP to IpAddress:Port (no browser dialog).
    ///  • Browser — a print-optimized KOT view is opened client-side and window.print() is called
    ///    (for USB/local printers attached to the POS machine).
    /// A printer serves a Station (e.g. Kitchen/Bar/Grill); items are routed by their category's station.
    /// </summary>
    public class KitchenPrinter : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required][StringLength(80)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Network | Browser</summary>
        [Required][StringLength(20)]
        public string ConnectionType { get; set; } = "Network";

        [StringLength(60)]
        public string? IpAddress { get; set; }

        public int Port { get; set; } = 9100;

        /// <summary>The station this printer serves, e.g. "Kitchen", "Bar", "Grill". Matched against Category.KotStation.</summary>
        [StringLength(40)]
        public string Station { get; set; } = "Kitchen";

        /// <summary>Fallback printer for items whose category has no station mapping (or an unmatched one).</summary>
        public bool IsDefault { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BranchId")]
        public Branch? Branch { get; set; }
    }

    /// <summary>Audit/troubleshooting record of every KOT dispatch (for reprints + diagnostics).</summary>
    public class KotPrintLog : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        /// <summary>Null for a printer test slip (not tied to an order).</summary>
        public int? OrderId { get; set; }

        public int? KitchenPrinterId { get; set; }

        [StringLength(40)]
        public string? Station { get; set; }

        [StringLength(80)]
        public string? PrinterName { get; set; }

        /// <summary>Printed | Browser | Queued | Failed | Test</summary>
        [Required][StringLength(20)]
        public string Status { get; set; } = "Printed";

        [StringLength(400)]
        public string? Message { get; set; }

        public int ItemCount { get; set; }

        public DateTime PrintedAt { get; set; } = DateTime.Now;

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        // (OrderId is nullable so a printer test slip can be logged without an order.)

        [ForeignKey("KitchenPrinterId")]
        public KitchenPrinter? KitchenPrinter { get; set; }
    }
}
