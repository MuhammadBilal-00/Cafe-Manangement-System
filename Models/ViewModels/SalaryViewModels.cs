using System;
using System.Collections.Generic;

namespace Cafe.Models.ViewModels
{
    public class SalaryIndexViewModel
    {
        public List<SalaryRecord> Records { get; set; } = new();
        public List<Branch> Branches { get; set; } = new();

        // Filters
        public int? BranchId { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public int Month { get; set; } = DateTime.Now.Month;
        public string? PaymentStatus { get; set; }
        public string? WorkflowStatus { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; } = 25;

        // Summary
        public decimal TotalBaseSalary { get; set; }
        public decimal TotalBonuses { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal TotalFinalSalary { get; set; }
        public decimal TotalOvertimePay { get; set; }
        public decimal TotalAttendanceBonus { get; set; }
        public int PendingCount { get; set; }
        public int PaidCount { get; set; }
        public int DraftCount { get; set; }
        public int FinalizedCount { get; set; }
    }

    public class SalaryGenerateViewModel
    {
        public int Year { get; set; } = DateTime.Now.Year;
        public int Month { get; set; } = DateTime.Now.Month;
        public int? BranchId { get; set; }
        public List<Branch> Branches { get; set; } = new();
    }

    public class PayslipViewModel
    {
        public SalaryRecord Record { get; set; } = null!;
        public List<Attendance> AttendanceDetails { get; set; } = new();
        public List<SalaryAdjustment> Adjustments { get; set; } = new();
    }
}
