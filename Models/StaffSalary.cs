using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class StaffSalary
    {
        public int Id { get; set; }

        [Required]
        public int StaffId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BaseSalary { get; set; }

        [Range(0, double.MaxValue)]
        public decimal HourlyRate { get; set; }

        [StringLength(20)]
        public string PaymentType { get; set; } = "Monthly"; // Monthly, Hourly, Weekly

        public DateTime EffectiveFromDate { get; set; }
        public DateTime? EffectiveToDate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Bonus { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Deductions { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int CreatedBy { get; set; }

        // Navigation Properties
        [ForeignKey("StaffId")]
        public Staff Staff { get; set; }

        [ForeignKey("CreatedBy")]
        public User CreatedByUser { get; set; }
    }

}
