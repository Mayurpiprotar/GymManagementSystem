using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using GymManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public class MembershipManagementController : Controller
{
    private readonly ApplicationDbContext _context;

    public MembershipManagementController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: MembershipManagement
    public async Task<IActionResult> Index()
    {
        var memberships = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.Payments)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync();

        // Calculate canonical status according to payment and date rules
        foreach (var m in memberships)
        {
            m.Status = MembershipStatusResolver.ResolveStatus(m);
        }

        return View(memberships);
    }

    // GET: MembershipManagement/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var membership = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        if (membership == null)
        {
            return NotFound();
        }

        membership.Status = MembershipStatusResolver.ResolveStatus(membership);

        return View(membership);
    }

    // GET: MembershipManagement/Create
    public async Task<IActionResult> Create()
    {
        var model = new MembershipViewModel
        {
            StartDate = DateTime.Today
        };

        await PopulateDropdownListsAsync(model);
        return View(model);
    }

    // POST: MembershipManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MembershipViewModel model)
    {
        ModelState.Remove("MemberList");
        ModelState.Remove("PlanList");
        ModelState.Remove("EndDate");
        ModelState.Remove("Status");

        var plan = await _context.MembershipPlans.FindAsync(model.MembershipPlanId);
        if (plan == null)
        {
            ModelState.AddModelError("MembershipPlanId", "The selected membership plan does not exist.");
        }

        var memberExists = await _context.Members.AnyAsync(m => m.MemberId == model.MemberId);
        if (!memberExists)
        {
            ModelState.AddModelError("MemberId", "The selected member does not exist.");
        }

        if (ModelState.IsValid && plan != null)
        {
            // Business rule: EndDate is calculated strictly server-side
            var calculatedEndDate = model.StartDate.AddMonths(plan.DurationInMonths);
            // Business rule: Status is calculated strictly server-side based on current date
            var calculatedStatus = CalculateMembershipStatus(model.StartDate, calculatedEndDate);

            var membership = new Membership
            {
                MemberId = model.MemberId,
                MembershipPlanId = model.MembershipPlanId,
                StartDate = model.StartDate,
                EndDate = calculatedEndDate,
                Status = calculatedStatus
            };

            _context.Memberships.Add(membership);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Membership was assigned and created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownListsAsync(model);
        return View(model);
    }

    // GET: MembershipManagement/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var membership = await _context.Memberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        if (membership == null)
        {
            return NotFound();
        }

        var currentStatus = CalculateMembershipStatus(membership.StartDate, membership.EndDate);

        var model = new MembershipViewModel
        {
            MembershipId = membership.MembershipId,
            MemberId = membership.MemberId,
            MembershipPlanId = membership.MembershipPlanId,
            StartDate = membership.StartDate,
            EndDate = membership.EndDate,
            Status = currentStatus
        };

        await PopulateDropdownListsAsync(model);
        return View(model);
    }

    // POST: MembershipManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MembershipViewModel model)
    {
        if (id != model.MembershipId)
        {
            return NotFound();
        }

        ModelState.Remove("MemberList");
        ModelState.Remove("PlanList");
        ModelState.Remove("EndDate");
        ModelState.Remove("Status");

        var plan = await _context.MembershipPlans.FindAsync(model.MembershipPlanId);
        if (plan == null)
        {
            ModelState.AddModelError("MembershipPlanId", "The selected membership plan does not exist.");
        }

        var memberExists = await _context.Members.AnyAsync(m => m.MemberId == model.MemberId);
        if (!memberExists)
        {
            ModelState.AddModelError("MemberId", "The selected member does not exist.");
        }

        if (ModelState.IsValid && plan != null)
        {
            var existingMembership = await _context.Memberships.FindAsync(id);
            if (existingMembership == null)
            {
                return NotFound();
            }

            var calculatedEndDate = model.StartDate.AddMonths(plan.DurationInMonths);
            var calculatedStatus = CalculateMembershipStatus(model.StartDate, calculatedEndDate);

            existingMembership.MemberId = model.MemberId;
            existingMembership.MembershipPlanId = model.MembershipPlanId;
            existingMembership.StartDate = model.StartDate;
            existingMembership.EndDate = calculatedEndDate;
            existingMembership.Status = calculatedStatus;

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Membership #{id} was updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await MembershipExistsAsync(model.MembershipId))
                {
                    return NotFound();
                }
                throw;
            }
        }

        await PopulateDropdownListsAsync(model);
        return View(model);
    }

    // GET: MembershipManagement/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var membership = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        if (membership == null)
        {
            return NotFound();
        }

        membership.Status = MembershipStatusResolver.ResolveStatus(membership);

        return View(membership);
    }

    // POST: MembershipManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var membership = await _context.Memberships
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        if (membership == null)
        {
            return NotFound();
        }

        // Referential integrity check (DeleteBehavior.Restrict with Payment)
        if (membership.Payments.Count > 0)
        {
            TempData["ErrorMessage"] = $"Cannot delete membership #{membership.MembershipId} because it has {membership.Payments.Count} associated payment record(s). Deletion is blocked to preserve data integrity.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        try
        {
            _context.Memberships.Remove(membership);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Membership #{id} was deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Unable to delete membership due to database constraints. Please check for related records.";
            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    /// <summary>
    /// Delegates to the centralized canonical MembershipStatusResolver.
    /// </summary>
    public static string CalculateMembershipStatus(DateTime startDate, DateTime endDate)
    {
        return MembershipStatusResolver.ResolveStatus(startDate, endDate, isPaid: true);
    }

    private async Task PopulateDropdownListsAsync(MembershipViewModel model)
    {
        var members = await _context.Members
            .AsNoTracking()
            .OrderBy(m => m.FullName)
            .ToListAsync();

        model.MemberList = members.Select(m => new SelectListItem
        {
            Value = m.MemberId.ToString(),
            Text = $"{m.FullName} — {m.Email}"
        });

        var plans = await _context.MembershipPlans
            .AsNoTracking()
            .OrderBy(p => p.DurationInMonths)
            .ThenBy(p => p.Price)
            .ToListAsync();

        model.PlanList = plans.Select(p => new SelectListItem
        {
            Value = p.MembershipPlanId.ToString(),
            Text = $"{p.Name} — {p.DurationInMonths} {(p.DurationInMonths == 1 ? "month" : "months")} — ${p.Price:F2}"
        });
    }

    private async Task<bool> MembershipExistsAsync(int id)
    {
        return await _context.Memberships.AnyAsync(e => e.MembershipId == id);
    }
}
