using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>Phase 4: a payment made to a supplier — drives down accounts payable (AP).</summary>
    public class SupplierPayment : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int SupplierId { get; set; }
        public int? BranchId { get; set; }

        [Column(TypeName = "decimal(10,2)")][Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
        [Required][StringLength(30)] public string Method { get; set; } = "Cash";
        [StringLength(100)] public string? Reference { get; set; }
        public DateTime PaidAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }

        [ForeignKey("SupplierId")] public Supplier? Supplier { get; set; }
    }
}
