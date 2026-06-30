using System;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public interface IBranchSettingService
    {
        /// <summary>Returns the branch's settings row, creating a default one on first access.</summary>
        Task<BranchSetting> GetOrCreateAsync(int branchId);

        Task<BranchSetting> UpdateAsync(int branchId, bool hardwareTerminalEnabled, decimal taxRatePercent, string? invoiceFooterNote);
    }

    public class BranchSettingService : IBranchSettingService
    {
        private readonly ApplicationDbContext _context;

        public BranchSettingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BranchSetting> GetOrCreateAsync(int branchId)
        {
            var setting = await _context.BranchSettings.FirstOrDefaultAsync(s => s.BranchId == branchId);
            if (setting != null)
                return setting;

            setting = new BranchSetting
            {
                BranchId = branchId,
                HardwareTerminalEnabled = false,
                TaxRatePercent = 0,
                UpdatedAt = DateTime.Now
            };
            _context.BranchSettings.Add(setting);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request created it first (unique index on BranchId) — load theirs.
                _context.Entry(setting).State = EntityState.Detached;
                setting = await _context.BranchSettings.FirstAsync(s => s.BranchId == branchId);
            }

            return setting;
        }

        public async Task<BranchSetting> UpdateAsync(int branchId, bool hardwareTerminalEnabled, decimal taxRatePercent, string? invoiceFooterNote)
        {
            var setting = await GetOrCreateAsync(branchId);
            setting.HardwareTerminalEnabled = hardwareTerminalEnabled;
            setting.TaxRatePercent = Math.Clamp(taxRatePercent, 0, 100);
            setting.InvoiceFooterNote = invoiceFooterNote;
            setting.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return setting;
        }
    }
}
