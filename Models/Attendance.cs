using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class Attendance
    {
        public int Id { get; set; }

        [Required]
        public int StaffId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? CheckInTime { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? CheckOutTime { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Present"; // Present, Absent, Late, Half-Day

        [Range(0, 1440)]
        public int LateMinutes { get; set; } = 0;

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? MarkedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("StaffId")]
        public Staff Staff { get; set; } = null!;

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        [ForeignKey("MarkedById")]
        public User? MarkedBy { get; set; }
    }
}
