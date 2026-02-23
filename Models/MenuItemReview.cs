using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class MenuItemReview
    {

        public int Id { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        public DateTime ReviewDate { get; set; } = DateTime.Now;

        public bool IsVerified { get; set; } = false; // Verified purchase

        // Navigation Properties
        [ForeignKey("MenuItemId")]
        public MenuItem MenuItem { get; set; } = null!;

        [ForeignKey("CustomerId")]
        public User Customer { get; set; } = null!;

    }
}
