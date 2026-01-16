using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Branch
            {
            public int Id { get; set; }

            [Required]
            [StringLength(100)]
            public string Name { get; set; }

            [Required]
            [StringLength(200)]
            public string Location { get; set; }

            [Required]
            [StringLength(20)]
            public string ContactInfo { get; set; }

            [StringLength(100)]
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
