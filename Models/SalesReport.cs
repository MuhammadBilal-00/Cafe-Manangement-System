using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class SalesReport
    {
        public int Id { get; set; }

        [Required]
        public int BranchId { get; set; }

        public DateTime ReportDate { get; set; } = DateTime.Now;

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalRevenue { get; set; }

        [Range(0, int.MaxValue)]
        public int TotalOrders { get; set; }

        [Range(0, double.MaxValue)]
        public decimal AverageOrderValue { get; set; }

        [StringLength(1000)]
        public string Summary { get; set; }

        // Navigation Properties
        [ForeignKey("BranchId")]
        public Branch Branch { get; set; }
    }
}
