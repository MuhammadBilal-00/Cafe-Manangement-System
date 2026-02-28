using Cafe.Data;
using Cafe.Attributes;
using Cafe.Helpers;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireStaffOrAbove]
    public class TodoController : BaseController
    {
        public TodoController(ApplicationDbContext context) : base(context) { }

        // GET: /Todo  – returns JSON list for the current user
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetUserId();
            if (userId == null) return Unauthorized();

            var items = await _context.TodoItems
                .Where(t => t.UserId == userId.Value)
                .OrderBy(t => t.IsCompleted)
                .ThenByDescending(t => t.Priority == "High" ? 3 : t.Priority == "Medium" ? 2 : 1)
                .ThenBy(t => t.DueDate)
                .ToListAsync();

            return Json(items);
        }

        // POST: /Todo/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TodoCreateDto dto)
        {
            var userId = HttpContext.Session.GetUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { error = "Title is required" });

            var item = new TodoItem
            {
                Title = dto.Title.Trim(),
                Priority = dto.Priority ?? "Medium",
                DueDate = dto.DueDate,
                UserId = userId.Value
            };

            _context.TodoItems.Add(item);
            await _context.SaveChangesAsync();

            return Json(item);
        }

        // PUT: /Todo/Toggle/5
        [HttpPut]
        public async Task<IActionResult> Toggle(int id)
        {
            var userId = HttpContext.Session.GetUserId();
            var item = await _context.TodoItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (item == null) return NotFound();

            item.IsCompleted = !item.IsCompleted;
            await _context.SaveChangesAsync();

            return Json(item);
        }

        // DELETE: /Todo/Delete/5
        [HttpDelete]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = HttpContext.Session.GetUserId();
            var item = await _context.TodoItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (item == null) return NotFound();

            _context.TodoItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }

    public class TodoCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Priority { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
