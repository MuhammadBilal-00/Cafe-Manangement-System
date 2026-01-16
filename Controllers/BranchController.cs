// Controllers/BranchController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Data;
using Cafe.Models;
using Cafe.Attributes;
using Cafe.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace Cafe.Controllers
{
    public class BranchController : BaseController
    {
        public BranchController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            IQueryable<Branch> branchesQuery = _context.Branches.Include(b => b.Manager);

            // Branch managers can only see their own branch
            if (HttpContext.Session.IsBranchManager())
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                {
                    branchesQuery = branchesQuery.Where(b => b.Id == managedBranchId.Value);
                }
            }

            var branches = await branchesQuery.ToListAsync();
            return View(branches);
        }


    [RequireOwner]
    public IActionResult Create()
    {
        ViewBag.Managers = _context.Users
            .Where(u => u.Role == "BranchManager")
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Name
            })
            .ToList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireOwner]
    public async Task<IActionResult> Create([Bind("Name,Location,ContactInfo,OpeningHours,ManagerId")] Branch branch)
    {
        if (ModelState.IsValid)
        {
            branch.CreatedDate = DateTime.Now;
            branch.IsActive = true;

            _context.Add(branch);
            await _context.SaveChangesAsync();

            SetSuccessMessage("Branch created successfully!");
            return RedirectToAction(nameof(Index));
        }

        // Fix: Convert to SelectListItem
        ViewBag.Managers = _context.Users
            .Where(u => u.Role == "BranchManager")
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Name
            })
            .ToList();
        return View(branch);
    }

    [RequireOwner]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var branch = await _context.Branches.FindAsync(id);
        if (branch == null)
        {
            return NotFound();
        }

        // Fix: Convert to SelectListItem
        ViewBag.Managers = _context.Users
            .Where(u => u.Role == "BranchManager")
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Name
            })
            .ToList();
        return View(branch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireOwner]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Location,ContactInfo,OpeningHours,ManagerId,IsActive")] Branch branch)
    {
        if (id != branch.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(branch);
                await _context.SaveChangesAsync();

                SetSuccessMessage("Branch updated successfully!");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BranchExists(branch.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // Fix: Convert to SelectListItem
        ViewBag.Managers = _context.Users
            .Where(u => u.Role == "BranchManager")
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Name
            })
            .ToList();
        return View(branch);
    }
    //[RequireOwner]
    //public IActionResult Create()
    //{
    //    ViewBag.Managers = _context.Users.Where(u => u.Role == "BranchManager").ToList();
    //    return View();
    //}

    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //[RequireOwner]
    //public async Task<IActionResult> Create([Bind("Name,Location,ContactInfo,OpeningHours,ManagerId")] Branch branch)
    //{
    //    if (ModelState.IsValid)
    //    {
    //        branch.CreatedDate = DateTime.Now;
    //        branch.IsActive = true;

    //        _context.Add(branch);
    //        await _context.SaveChangesAsync();

    //        SetSuccessMessage("Branch created successfully!");
    //        return RedirectToAction(nameof(Index));
    //    }

    //    ViewBag.Managers = _context.Users.Where(u => u.Role == "BranchManager").ToList();
    //    return View(branch);
    //}

    public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .Include(b => b.Manager)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (branch == null)
            {
                return NotFound();
            }

            if (!CanAccessBranch(branch.Id))
            {
                return AccessDenied();
            }

            return View(branch);
        }

        //[RequireOwner]
        //public async Task<IActionResult> Edit(int? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var branch = await _context.Branches.FindAsync(id);
        //    if (branch == null)
        //    {
        //        return NotFound();
        //    }

        //    ViewBag.Managers = _context.Users.Where(u => u.Role == "BranchManager").ToList();
        //    return View(branch);
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[RequireOwner]
        //public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Location,ContactInfo,OpeningHours,ManagerId,IsActive")] Branch branch)
        //{
        //    if (id != branch.Id)
        //    {
        //        return NotFound();
        //    }

        //    if (ModelState.IsValid)
        //    {
        //        try
        //        {
        //            _context.Update(branch);
        //            await _context.SaveChangesAsync();

        //            SetSuccessMessage("Branch updated successfully!");
        //        }
        //        catch (DbUpdateConcurrencyException)
        //        {
        //            if (!BranchExists(branch.Id))
        //            {
        //                return NotFound();
        //            }
        //            else
        //            {
        //                throw;
        //            }
        //        }
        //        return RedirectToAction(nameof(Index));
        //    }

        //    ViewBag.Managers = _context.Users.Where(u => u.Role == "BranchManager").ToList();
        //    return View(branch);
        //}

        //[RequireOwner]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .Include(b => b.Manager)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (branch == null)
            {
                return NotFound();
            }

            return View(branch);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch != null)
            {
                // Soft delete by setting IsActive to false
                branch.IsActive = false;
                _context.Branches.Update(branch);
                await _context.SaveChangesAsync();

                SetSuccessMessage("Branch deleted successfully!");
            }

            return RedirectToAction(nameof(Index));
        }

        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.Id == id);
        }
    }
}