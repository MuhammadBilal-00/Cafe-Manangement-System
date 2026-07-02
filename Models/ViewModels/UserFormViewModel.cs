using System.ComponentModel.DataAnnotations;

namespace Cafe.Models.ViewModels
{
    /// <summary>
    /// Admin-only user provisioning form (create/edit). Deliberately NOT the User entity:
    /// the password travels as plain text into the hasher and the hash is never bound or shown.
    /// </summary>
    public class UserFormViewModel
    {
        public int? Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Role { get; set; } = string.Empty;

        /// <summary>Branch assignment: managed branch for BranchManager, work branch for staff-level roles.</summary>
        public int? BranchId { get; set; }

        /// <summary>Initial password — required on create only; ignored on edit (use Reset Password).</summary>
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string? Password { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>Row projection for the admin user list (never carries the password hash).</summary>
    public class UserRowViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? BranchName { get; set; }
    }
}
