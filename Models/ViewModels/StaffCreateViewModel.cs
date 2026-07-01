using System.ComponentModel.DataAnnotations;

namespace Cafe.Models.ViewModels
{
    public class StaffCreateViewModel
    {
        // User properties
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string Phone { get; set; } = "";

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        // Staff properties
        [Required(ErrorMessage = "Staff role is required")]
        public int StaffRoleId { get; set; }

        [Required(ErrorMessage = "Branch is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "Employment type is required")]
        public string EmploymentType { get; set; } = "Full-time";

        [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
        public string? Department { get; set; }

        // Phase 7/10: structured department & designation (org chart) alongside the legacy free-text field.
        public int? DepartmentId { get; set; }
        public int? DesignationId { get; set; }

        [StringLength(100, ErrorMessage = "Employee ID cannot exceed 100 characters")]
        public string? EmployeeId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}