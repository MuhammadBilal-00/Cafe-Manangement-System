using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>Info, Success, Warning, Error</summary>
        [Required]
        [StringLength(20)]
        public string Type { get; set; } = "Info";

        /// <summary>Specific user target (null = not user-specific)</summary>
        public int? UserId { get; set; }

        /// <summary>Role-based target: Owner, BranchManager, Staff (null = not role-specific)</summary>
        [StringLength(50)]
        public string? RoleTarget { get; set; }

        /// <summary>Branch-based target (null = all branches)</summary>
        public int? BranchId { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UserId of the person who triggered the notification</summary>
        public int? CreatedBy { get; set; }

        /// <summary>URL to redirect when notification is clicked</summary>
        [StringLength(500)]
        public string? RedirectUrl { get; set; }

        /// <summary>Icon class for display (e.g. fas fa-receipt)</summary>
        [StringLength(100)]
        public string? Icon { get; set; }

        // Navigation
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("BranchId")]
        public Branch? Branch { get; set; }

        [ForeignKey("CreatedBy")]
        public User? Creator { get; set; }
    }
}
