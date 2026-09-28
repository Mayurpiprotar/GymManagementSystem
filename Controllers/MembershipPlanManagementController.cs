using GymManagementSystem.Data;
using GymManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public class MembershipPlanManagementController : Controller
{
    private readonly ApplicationDbContext _context;

    public MembershipPlanManagementController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: MembershipPlanManagement
    public async Task<IActionResult> Index()
    {
        var plans = await _context.MembershipPlans
            .AsNoTracking()
            .Include(p => p.Memberships)
            .OrderBy(p => p.DurationInMonths)
            .ThenBy(p => p.Price)
            .ToListAsync();

        return View(plans);
    }

    // GET: MembershipPlanManagement/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var plan = await _context.MembershipPlans
            .AsNoTracking()
            .Include(p => p.Memberships)
            .FirstOrDefaultAsync(p => p.MembershipPlanId == id);

        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    // GET: MembershipPlanManagement/Create
    public IActionResult Create()
    {
        var plan = new MembershipPlan
        {
            DurationInMonths = 1,
            Price = 0.00m
        };

        return View(plan);
    }

    // POST: MembershipPlanManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,DurationInMonths,Price,Description")] MembershipPlan plan)
    {
        ModelState.Remove("Memberships");

        if (ModelState.IsValid)
        {
            _context.MembershipPlans.Add(plan);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Membership plan '{plan.Name}' was created successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(plan);
    }

    // GET: MembershipPlanManagement/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var plan = await _context.MembershipPlans.FindAsync(id);
        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    // POST: MembershipPlanManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("MembershipPlanId,Name,DurationInMonths,Price,Description")] MembershipPlan plan)
    {
        if (id != plan.MembershipPlanId)
        {
            return NotFound();
        }

        ModelState.Remove("Memberships");

        if (ModelState.IsValid)
        {
            var existingPlan = await _context.MembershipPlans.FindAsync(id);
            if (existingPlan == null)
            {
                return NotFound();
            }

            existingPlan.Name = plan.Name;
            existingPlan.DurationInMonths = plan.DurationInMonths;
            existingPlan.Price = plan.Price;
            existingPlan.Description = plan.Description;

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Membership plan '{existingPlan.Name}' was updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await MembershipPlanExistsAsync(plan.MembershipPlanId))
                {
                    return NotFound();
                }
                throw;
            }
        }

        return View(plan);
    }

    // GET: MembershipPlanManagement/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var plan = await _context.MembershipPlans
            .AsNoTracking()
            .Include(p => p.Memberships)
            .FirstOrDefaultAsync(p => p.MembershipPlanId == id);

        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    // POST: MembershipPlanManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var plan = await _context.MembershipPlans
            .Include(p => p.Memberships)
            .FirstOrDefaultAsync(p => p.MembershipPlanId == id);

        if (plan == null)
        {
            return NotFound();
        }

        // Referential integrity check (DeleteBehavior.Restrict)
        if (plan.Memberships.Count > 0)
        {
            TempData["ErrorMessage"] = $"Cannot delete membership plan '{plan.Name}' because it has {plan.Memberships.Count} associated membership record(s). Deletion is blocked to preserve data integrity.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        try
        {
            _context.MembershipPlans.Remove(plan);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Membership plan '{plan.Name}' was deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Unable to delete membership plan due to database constraints. Please check for related records.";
            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    private async Task<bool> MembershipPlanExistsAsync(int id)
    {
        return await _context.MembershipPlans.AnyAsync(e => e.MembershipPlanId == id);
    }
}
