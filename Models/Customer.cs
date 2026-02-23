using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Range(0, int.MaxValue)]
        public int LoyaltyPoints { get; set; } = 0;

        public DateTime JoinDate { get; set; } = DateTime.Now;

        [StringLength(200)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}
