using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Branch
            {
            public int Id { get; set; }

            [Required]
            [StringLength(100)]
            public string Name { get; set; } = string.Empty;

            [Required]
            [StringLength(200)]
            public string Location { get; set; } = string.Empty;

            [Required]
            [StringLength(20)]
            [Display(Name = "Contact Info")]
            public string ContactInfo { get; set; } = string.Empty;

            [StringLength(100)]
            [Display(Name = "Opening Hours")]
            public string? OpeningHours { get; set; }

            public int? ManagerId { get; set; }

            public DateTime CreatedDate { get; set; } = DateTime.Now;
            public bool IsActive { get; set; } = true;

            // Navigation Properties
            [ForeignKey("ManagerId")]
            public User? Manager { get; set; }

            public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
            public ICollection<Order> Orders { get; set; } = new List<Order>();
            public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
            public ICollection<Staff> Staff { get; set; } = new List<Staff>();
            public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
        }

    }
