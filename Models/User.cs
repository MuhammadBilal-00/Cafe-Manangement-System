using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    // User Management Models
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Role { get; set; } = string.Empty; // Owner, BranchManager, Staff, Customer

        [StringLength(255)]
        public string? PasswordHash { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        public ICollection<Branch> ManagedBranches { get; set; } = new List<Branch>();
        public ICollection<Staff> StaffRecords { get; set; } = new List<Staff>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }
}