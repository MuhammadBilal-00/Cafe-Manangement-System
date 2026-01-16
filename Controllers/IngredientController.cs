using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class IngredientController : BaseController
    {
        public IngredientController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index(string search, bool showAllergens = false)
        {
            var ingredientsQuery = _context.Ingredients.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                ingredientsQuery = ingredientsQuery.Where(i =>
                    i.Name.Contains(search) ||
                    i.Description.Contains(search) ||
                    i.Supplier.Contains(search));
            }

            if (showAllergens)
            {
                ingredientsQuery = ingredientsQuery.Where(i => i.IsAllergen);
            }

            ViewBag.Search = search;
            ViewBag.ShowAllergens = showAllergens;

            return View(await ingredientsQuery.Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync());
        }

        public IActionResult Create()
        {
            ViewBag.AllergenTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Gluten", Text = "Gluten" },
                new SelectListItem { Value = "Dairy", Text = "Dairy" },
                new SelectListItem { Value = "Eggs", Text = "Eggs" },
                new SelectListItem { Value = "Fish", Text = "Fish" },
                new SelectListItem { Value = "Shellfish", Text = "Shellfish" },
                new SelectListItem { Value = "Tree Nuts", Text = "Tree Nuts" },
                new SelectListItem { Value = "Peanuts", Text = "Peanuts" },
                new SelectListItem { Value = "Soy", Text = "Soy" },
                new SelectListItem { Value = "Sesame", Text = "Sesame" }
            };
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Unit,CostPerUnit,Supplier,IsAllergen,AllergenType")] Ingredient ingredient)
        {
            if (ModelState.IsValid)
            {
                _context.Add(ingredient);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ingredient created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(ingredient);
        }

        private bool IngredientExists(int id)
        {
            return _context.Ingredients.Any(e => e.Id == id);
        }
    }
}


