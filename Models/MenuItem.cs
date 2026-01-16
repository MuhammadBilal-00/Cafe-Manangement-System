using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class MenuItem
    {

        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(1000)]
        public string? LongDescription { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? OriginalPrice { get; set; } // For discount display

        [Range(0, double.MaxValue)]
        public decimal CostPrice { get; set; } // Cost to make the item

        public bool Availability { get; set; } = true;

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int BranchId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastUpdated { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [StringLength(1000)]
        public string? ImageGallery { get; set; } // JSON array of image URLs

        // Nutritional Information
        [Range(0, int.MaxValue)]
        public int? Calories { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Protein { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Carbohydrates { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Fat { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Fiber { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Sugar { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Sodium { get; set; }

        // Dietary Flags
        public bool IsVegetarian { get; set; } = false;
        public bool IsVegan { get; set; } = false;
        public bool IsGlutenFree { get; set; } = false;
        public bool IsDairyFree { get; set; } = false;
        public bool IsNutFree { get; set; } = false;
        public bool IsSpicy { get; set; } = false;

        [Range(0, 5)]
        public int? SpiceLevel { get; set; }

        // Business Logic
        [Range(0, int.MaxValue)]
        public int PreparationTime { get; set; } = 15; // minutes

        [Range(1, 5)]
        public decimal? AverageRating { get; set; }

        [Range(0, int.MaxValue)]
        public int TotalRatings { get; set; } = 0;

        [Range(0, int.MaxValue)]
        public int PopularityScore { get; set; } = 0;

        public bool IsFeatured { get; set; } = false;
        public bool IsSpecial { get; set; } = false; // Daily special

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; } = 0;

        [StringLength(100)]
        public string? Tags { get; set; } // Comma-separated tags

        // Navigation Properties
        [ForeignKey("CategoryId")]
        public Category Category { get; set; }

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<MenuItemIngredient> Ingredients { get; set; } = new List<MenuItemIngredient>();
        public ICollection<MenuItemReview> Reviews { get; set; } = new List<MenuItemReview>();
    }
}
 

