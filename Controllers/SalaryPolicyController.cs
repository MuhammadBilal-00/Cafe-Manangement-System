using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    [RequireOwner]
    public class SalaryPolicyController : BaseController
    {
        private readonly ISalaryPolicyService _policyService;

        public SalaryPolicyController(
            ApplicationDbContext context,
            ISalaryPolicyService policyService) : base(context)
        {
            _policyService = policyService;
        }

        // GET: SalaryPolicy
        public async Task<IActionResult> Index()
        {
            var policies = await _policyService.GetAllPoliciesAsync();
            var active = await _policyService.GetActivePolicyAsync();

            var vm = new SalaryPolicyViewModel
            {
                Policies = policies,
                ActivePolicy = active
            };

            return View(vm);
        }

        // GET: SalaryPolicy/Create
        public IActionResult Create()
        {
            var policy = new SalaryPolicy
            {
                EffectiveFrom = DateTime.Today,
                IsActive = true
            };
            return View(policy);
        }

        // POST: SalaryPolicy/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalaryPolicy policy)
        {
            if (!ModelState.IsValid) return View(policy);

            var userId = GetCurrentUserId();
            if (!userId.HasValue) return AccessDenied();

            try
            {
                await _policyService.CreatePolicyAsync(policy, userId.Value);
                SetSuccessMessage($"Salary policy '{policy.Name}' created successfully!");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                SetErrorMessage(ex.Message);
                return View(policy);
            }
        }

        // GET: SalaryPolicy/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var policy = await _policyService.GetPolicyAsync(id);
            if (policy == null) return NotFound();

            return View(policy);
        }

        // POST: SalaryPolicy/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SalaryPolicy policy)
        {
            if (id != policy.Id) return BadRequest();
            if (!ModelState.IsValid) return View(policy);

            var userId = GetCurrentUserId();
            if (!userId.HasValue) return AccessDenied();

            try
            {
                var updated = await _policyService.UpdatePolicyAsync(policy, userId.Value);
                if (updated == null)
                {
                    SetErrorMessage("Policy not found.");
                    return RedirectToAction(nameof(Index));
                }

                SetSuccessMessage($"Policy '{policy.Name}' updated.");
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                SetErrorMessage(ex.Message);
                return View(policy);
            }
        }

        // POST: SalaryPolicy/Activate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return AccessDenied();

            var success = await _policyService.ActivatePolicyAsync(id, userId.Value);
            if (success)
                SetSuccessMessage("Policy activated! Overlapping policies have been deactivated.");
            else
                SetErrorMessage("Failed to activate policy.");

            return RedirectToAction(nameof(Index));
        }

        // POST: SalaryPolicy/Deactivate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return AccessDenied();

            var success = await _policyService.DeactivatePolicyAsync(id, userId.Value);
            if (success)
                SetSuccessMessage("Policy deactivated.");
            else
                SetErrorMessage("Failed to deactivate policy.");

            return RedirectToAction(nameof(Index));
        }
    }
}
