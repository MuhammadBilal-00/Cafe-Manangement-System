using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public class SalaryPolicyService : ISalaryPolicyService
    {
        private readonly ApplicationDbContext _context;

        public SalaryPolicyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<SalaryPolicy>> GetAllPoliciesAsync()
        {
            return await _context.SalaryPolicies
                .Include(p => p.CreatedBy)
                .Include(p => p.UpdatedBy)
                .OrderByDescending(p => p.IsActive)
                .ThenByDescending(p => p.EffectiveFrom)
                .ToListAsync();
        }

        public async Task<SalaryPolicy?> GetPolicyAsync(int id)
        {
            return await _context.SalaryPolicies
                .Include(p => p.CreatedBy)
                .Include(p => p.UpdatedBy)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<SalaryPolicy?> GetActivePolicyAsync()
        {
            return await _context.SalaryPolicies
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();
        }

        public async Task<SalaryPolicy> CreatePolicyAsync(SalaryPolicy policy, int createdById)
        {
            // Validate no overlapping active policies
            if (policy.IsActive)
            {
                await DeactivateOverlappingPoliciesAsync(policy.EffectiveFrom, policy.EffectiveTo, excludeId: null);
            }

            policy.CreatedById = createdById;
            policy.CreatedAt = DateTime.Now;

            _context.SalaryPolicies.Add(policy);
            await _context.SaveChangesAsync();
            return policy;
        }

        public async Task<SalaryPolicy?> UpdatePolicyAsync(SalaryPolicy updated, int updatedById)
        {
            var existing = await _context.SalaryPolicies.FindAsync(updated.Id);
            if (existing == null) return null;

            // Check if this policy is used by any finalized salary records
            var usedByFinalized = await _context.SalaryRecords
                .AnyAsync(sr => sr.PolicyIdUsed == existing.Id && sr.Status != "Draft");

            if (usedByFinalized)
                throw new InvalidOperationException(
                    "Cannot modify a policy that has been used in finalized salary records. Create a new policy instead.");

            // Update fields
            existing.Name = updated.Name;
            existing.AbsenceDeductionFactor = updated.AbsenceDeductionFactor;
            existing.HalfDayDeductionFactor = updated.HalfDayDeductionFactor;
            existing.LatePenaltyThreshold = updated.LatePenaltyThreshold;
            existing.LatePenaltyFactor = updated.LatePenaltyFactor;
            existing.OvertimeMultiplier = updated.OvertimeMultiplier;
            existing.AttendanceBonusPercentage = updated.AttendanceBonusPercentage;
            existing.MaxLateForBonus = updated.MaxLateForBonus;
            existing.MaxAbsentForBonus = updated.MaxAbsentForBonus;
            existing.StandardDailyHours = updated.StandardDailyHours;
            existing.LateThresholdMinutes = updated.LateThresholdMinutes;
            existing.EffectiveFrom = updated.EffectiveFrom;
            existing.EffectiveTo = updated.EffectiveTo;
            existing.Notes = updated.Notes;
            existing.UpdatedById = updatedById;
            existing.UpdatedAt = DateTime.Now;

            if (updated.IsActive && !existing.IsActive)
            {
                await DeactivateOverlappingPoliciesAsync(existing.EffectiveFrom, existing.EffectiveTo, existing.Id);
            }
            existing.IsActive = updated.IsActive;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> ActivatePolicyAsync(int id, int userId)
        {
            var policy = await _context.SalaryPolicies.FindAsync(id);
            if (policy == null || policy.IsActive) return false;

            // Deactivate others that overlap
            await DeactivateOverlappingPoliciesAsync(policy.EffectiveFrom, policy.EffectiveTo, id);

            policy.IsActive = true;
            policy.UpdatedById = userId;
            policy.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivatePolicyAsync(int id, int userId)
        {
            var policy = await _context.SalaryPolicies.FindAsync(id);
            if (policy == null || !policy.IsActive) return false;

            policy.IsActive = false;
            policy.UpdatedById = userId;
            policy.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Deactivate all active policies whose date ranges overlap with the given range.
        /// </summary>
        private async Task DeactivateOverlappingPoliciesAsync(DateTime from, DateTime? to, int? excludeId)
        {
            var overlapping = await _context.SalaryPolicies
                .Where(p => p.IsActive
                    && (excludeId == null || p.Id != excludeId)
                    && p.EffectiveFrom <= (to ?? DateTime.MaxValue)
                    && (p.EffectiveTo == null || p.EffectiveTo >= from))
                .ToListAsync();

            foreach (var p in overlapping)
            {
                p.IsActive = false;
                p.UpdatedAt = DateTime.Now;
            }
        }
    }
}
