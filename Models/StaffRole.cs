using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    // New Role Management Model
    public class StaffRole
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string RoleName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal DefaultHourlyRate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal DefaultMonthlySalary { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsSystemRole { get; set; } = false; // Prevents deletion of core roles

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? CreatedBy { get; set; } // User who created this role

        // Navigation Properties
        [ForeignKey("CreatedBy")]
        public User? CreatedByUser { get; set; }

        public ICollection<Staff> StaffMembers { get; set; } = new List<Staff>();
    }
}
