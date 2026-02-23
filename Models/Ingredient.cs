using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Ingredient
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } = "g"; // grams, ml, pieces, etc.

        [Range(0, double.MaxValue)]
        public decimal CostPerUnit { get; set; }

        [StringLength(100)]
        public string? Supplier { get; set; }

        public bool IsAllergen { get; set; } = false;

        [StringLength(200)]
        public string? AllergenType { get; set; } // Gluten, Dairy, Nuts, etc.

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        public ICollection<MenuItemIngredient> MenuItemIngredients { get; set; } = new List<MenuItemIngredient>();
    }

}
