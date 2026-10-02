using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using GymManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

[Authorize(Roles = "Trainer")]
public class TrainerWorkoutPlanController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TrainerWorkoutPlanController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: TrainerWorkoutPlan
    public async Task<IActionResult> Index()
    {
        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return View(new List<WorkoutPlan>());
        }

        var plans = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Member)
            .Where(wp => wp.TrainerId == trainer.TrainerId)
            .OrderByDescending(wp => wp.CreatedDate)
            .ThenByDescending(wp => wp.WorkoutPlanId)
            .ToListAsync();

        return View(plans);
    }

    // GET: TrainerWorkoutPlan/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return NotFound();
        }

        // Must belong to authenticated trainer
        var plan = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Member)
            .Include(wp => wp.Trainer)
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id && wp.TrainerId == trainer.TrainerId);

        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    // GET: TrainerWorkoutPlan/Create
    public async Task<IActionResult> Create(int? memberId)
    {
        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return NotFound("Trainer profile not found.");
        }

        var model = new WorkoutPlanViewModel
        {
            CreatedDate = DateTime.Today,
            TrainerName = trainer.FullName,
            MemberId = memberId ?? 0
        };

        await PopulateAssignedActiveMembersListAsync(model, trainer.TrainerId);
        return View(model);
    }

    // POST: TrainerWorkoutPlan/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkoutPlanViewModel model)
    {
        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return NotFound("Trainer profile not found.");
        }

        ModelState.Remove(nameof(model.MemberList));
        ModelState.Remove(nameof(model.TrainerName));

        // Enforce: Member must have an Active, Paid membership assigned to this trainer
        var today = DateTime.Today;
        var eligible = await IsMemberEligibleForTrainerAsync(model.MemberId, trainer.TrainerId, today);
        if (!eligible)
        {
            ModelState.AddModelError(nameof(model.MemberId),
                "You can only prescribe workout plans for active, paid members currently assigned to you.");
        }

        if (ModelState.IsValid)
        {
            var plan = new WorkoutPlan
            {
                MemberId = model.MemberId,
                TrainerId = trainer.TrainerId, // STRICTLY derived from server-side authenticated Trainer
                PlanName = model.PlanName.Trim(),
                Description = model.Description.Trim(),
                CreatedDate = DateTime.Today // Server-controlled
            };

            _context.WorkoutPlans.Add(plan);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Workout plan '{plan.PlanName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        model.TrainerName = trainer.FullName;
        await PopulateAssignedActiveMembersListAsync(model, trainer.TrainerId);
        return View(model);
    }

    // GET: TrainerWorkoutPlan/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return NotFound();
        }

        // Verify plan belongs to current trainer
        var plan = await _context.WorkoutPlans
            .Include(wp => wp.Member)
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id && wp.TrainerId == trainer.TrainerId);

        if (plan == null)
        {
            return NotFound();
        }

        // Verify that the assigned member/membership is still active & paid
        var today = DateTime.Today;
        var eligible = await IsMemberEligibleForTrainerAsync(plan.MemberId, trainer.TrainerId, today);
        if (!eligible)
        {
            TempData["ErrorMessage"] = "Cannot edit this workout plan because the member's assigned membership is no longer active and paid.";
            return RedirectToAction(nameof(Index));
        }

        var model = new WorkoutPlanViewModel
        {
            WorkoutPlanId = plan.WorkoutPlanId,
            MemberId = plan.MemberId,
            PlanName = plan.PlanName,
            Description = plan.Description,
            CreatedDate = plan.CreatedDate,
            TrainerName = trainer.FullName
        };

        await PopulateAssignedActiveMembersListAsync(model, trainer.TrainerId);
        return View(model);
    }

    // POST: TrainerWorkoutPlan/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, WorkoutPlanViewModel model)
    {
        if (id != model.WorkoutPlanId)
        {
            return NotFound();
        }

        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return NotFound();
        }

        ModelState.Remove(nameof(model.MemberList));
        ModelState.Remove(nameof(model.TrainerName));

        var plan = await _context.WorkoutPlans
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id && wp.TrainerId == trainer.TrainerId);

        if (plan == null)
        {
            return NotFound();
        }

        // Verify that the target member is assigned to this trainer and is Active & Paid
        var today = DateTime.Today;
        var eligible = await IsMemberEligibleForTrainerAsync(model.MemberId, trainer.TrainerId, today);
        if (!eligible)
        {
            ModelState.AddModelError(nameof(model.MemberId),
                "You can only assign workout plans to active, paid members currently assigned to you.");
        }

        if (ModelState.IsValid)
        {
            plan.MemberId = model.MemberId;
            plan.PlanName = model.PlanName.Trim();
            plan.Description = model.Description.Trim();
            // TrainerId and CreatedDate are strictly preserved

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Workout plan '{plan.PlanName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        model.TrainerName = trainer.FullName;
        model.CreatedDate = plan.CreatedDate;
        await PopulateAssignedActiveMembersListAsync(model, trainer.TrainerId);
        return View(model);
    }

    // GET: TrainerWorkoutPlan/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return NotFound();
        }

        var plan = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Member)
            .Include(wp => wp.Trainer)
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id && wp.TrainerId == trainer.TrainerId);

        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    // POST: TrainerWorkoutPlan/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return NotFound();
        }

        var plan = await _context.WorkoutPlans
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id && wp.TrainerId == trainer.TrainerId);

        if (plan == null)
        {
            return NotFound();
        }

        _context.WorkoutPlans.Remove(plan);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Workout plan '{plan.PlanName}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    // =========================================================================
    // AUTHORIZATION & DATA HELPERS
    // =========================================================================

    private async Task<Trainer?> GetCurrentTrainerAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return null;
        }

        return await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == user.Id && t.IsVerified);
    }

    private async Task<bool> IsMemberEligibleForTrainerAsync(int memberId, int trainerId, DateTime today)
    {
        var memberships = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Payments)
            .Where(m => m.MemberId == memberId && m.AssignedTrainerId == trainerId)
            .ToListAsync();

        return memberships.Any(m =>
            MembershipStatusResolver.ResolveStatus(m, today) == GymConstants.MembershipStatuses.Active
            && m.Payments != null && m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid));
    }

    private async Task PopulateAssignedActiveMembersListAsync(WorkoutPlanViewModel model, int trainerId)
    {
        var today = DateTime.Today;
        var memberships = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.Payments)
            .Where(m => m.AssignedTrainerId == trainerId && m.Member != null)
            .ToListAsync();

        var activeAssignedMembers = memberships
            .Where(m => MembershipStatusResolver.ResolveStatus(m, today) == GymConstants.MembershipStatuses.Active
                     && m.Payments != null && m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid))
            .Select(m => m.Member!)
            .GroupBy(m => m.MemberId)
            .Select(g => g.First())
            .OrderBy(m => m.FullName)
            .ToList();

        model.MemberList = activeAssignedMembers.Select(m => new SelectListItem
        {
            Value = m.MemberId.ToString(),
            Text = $"{m.FullName} ({m.Email})"
        });
    }
}
