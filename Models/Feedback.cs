using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Feedback
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comments { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public bool IsResolved { get; set; } = false;

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public User Customer { get; set; }

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; }
    }
}
