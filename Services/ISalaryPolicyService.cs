using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models;

namespace Cafe.Services
{
    public interface ISalaryPolicyService
    {
        Task<List<SalaryPolicy>> GetAllPoliciesAsync();
        Task<SalaryPolicy?> GetPolicyAsync(int id);
        Task<SalaryPolicy?> GetActivePolicyAsync();
        Task<SalaryPolicy> CreatePolicyAsync(SalaryPolicy policy, int createdById);
        Task<SalaryPolicy?> UpdatePolicyAsync(SalaryPolicy policy, int updatedById);
        Task<bool> ActivatePolicyAsync(int id, int userId);
        Task<bool> DeactivatePolicyAsync(int id, int userId);
    }
}
