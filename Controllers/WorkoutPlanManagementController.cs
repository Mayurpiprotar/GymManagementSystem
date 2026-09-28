using GymManagementSystem.Data;
using GymManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public class WorkoutPlanManagementController : Controller
{
    private readonly ApplicationDbContext _context;

    public WorkoutPlanManagementController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: WorkoutPlanManagement
    public async Task<IActionResult> Index()
    {
        var plans = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Member)
            .Include(wp => wp.Trainer)
            .OrderByDescending(wp => wp.CreatedDate)
            .ThenByDescending(wp => wp.WorkoutPlanId)
            .ToListAsync();

        return View(plans);
    }

    // GET: WorkoutPlanManagement/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var plan = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Member)
            .Include(wp => wp.Trainer)
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id);

        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    // GET: WorkoutPlanManagement/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var plan = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Member)
            .Include(wp => wp.Trainer)
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id);

        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    // POST: WorkoutPlanManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var plan = await _context.WorkoutPlans.FindAsync(id);
        if (plan == null)
        {
            return NotFound();
        }

        _context.WorkoutPlans.Remove(plan);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Workout plan '{plan.PlanName}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}
