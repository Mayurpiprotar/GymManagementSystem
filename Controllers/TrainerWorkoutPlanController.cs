using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
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
    public async Task<IActionResult> Create()
    {
        var trainer = await GetCurrentTrainerAsync();
        if (trainer == null)
        {
            return NotFound("Trainer profile not found.");
        }

        var model = new WorkoutPlanViewModel
        {
            CreatedDate = DateTime.Today,
            TrainerName = trainer.FullName
        };

        await PopulateMemberListAsync(model);
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

        var memberExists = await _context.Members.AnyAsync(m => m.MemberId == model.MemberId);
        if (!memberExists)
        {
            ModelState.AddModelError(nameof(model.MemberId), "The selected member does not exist.");
        }

        if (ModelState.IsValid)
        {
            var plan = new WorkoutPlan
            {
                MemberId = model.MemberId,
                TrainerId = trainer.TrainerId, // Enforce authenticated trainer ownership
                PlanName = model.PlanName.Trim(),
                Description = model.Description.Trim(),
                CreatedDate = DateTime.Today // Controlled server-side
            };

            _context.WorkoutPlans.Add(plan);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Workout plan '{plan.PlanName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        model.TrainerName = trainer.FullName;
        await PopulateMemberListAsync(model);
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

        var plan = await _context.WorkoutPlans
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id && wp.TrainerId == trainer.TrainerId);

        if (plan == null)
        {
            return NotFound();
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

        await PopulateMemberListAsync(model);
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

        var memberExists = await _context.Members.AnyAsync(m => m.MemberId == model.MemberId);
        if (!memberExists)
        {
            ModelState.AddModelError(nameof(model.MemberId), "The selected member does not exist.");
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
        await PopulateMemberListAsync(model);
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

    private async Task<Trainer?> GetCurrentTrainerAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return null;
        }

        return await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == user.Id);
    }

    private async Task PopulateMemberListAsync(WorkoutPlanViewModel model)
    {
        var members = await _context.Members
            .AsNoTracking()
            .OrderBy(m => m.FullName)
            .ToListAsync();

        model.MemberList = members.Select(m => new SelectListItem
        {
            Value = m.MemberId.ToString(),
            Text = $"{m.FullName} ({m.Email})"
        });
    }
}
