using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? ImageUrl { get; set; }

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string? Color { get; set; } // For UI theming

        [StringLength(50)]
        public string? Icon { get; set; } // FontAwesome icon class

        // Navigation Properties
        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

    }
}
