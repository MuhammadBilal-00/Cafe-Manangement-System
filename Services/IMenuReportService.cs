using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models.ViewModels;

namespace Cafe.Services
{
    /// <summary>
    /// Service for generating Menu Performance reports.
    /// All heavy LINQ / business logic lives here, NOT in the controller.
    /// </summary>
    public interface IMenuReportService
    {
        /// <summary>
        /// Generates the full Menu Performance report for the given filters.
        /// Role-based branch restriction is enforced inside.
        /// </summary>
        Task<MenuPerformanceViewModel> GenerateReportAsync(
            DateTime from,
            DateTime to,
            int? branchId,
            int? categoryId,
            int topN,
            string userRole,
            int? userBranchId);

        /// <summary>
        /// Returns the raw item rows for export (same filtering).
        /// </summary>
        Task<List<MenuItemPerformanceRow>> GetExportRowsAsync(
            DateTime from,
            DateTime to,
            int? branchId,
            int? categoryId,
            string userRole,
            int? userBranchId);
    }
}
