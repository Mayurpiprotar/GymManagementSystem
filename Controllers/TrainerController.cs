using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

[Authorize(Roles = "Trainer")]
public class TrainerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TrainerController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        // Look up the Trainer record linked to the logged-in Identity user
        var trainer = await _context.Trainers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == user.Id);

        if (trainer == null)
        {
            return View(new TrainerDashboardViewModel
            {
                TrainerName = user.FullName ?? user.UserName ?? "Trainer",
                Email = user.Email ?? string.Empty
            });
        }

        // Count workout plans assigned to this trainer
        var assignedWorkoutPlansCount = await _context.WorkoutPlans
            .CountAsync(wp => wp.TrainerId == trainer.TrainerId);

        // Count unique members assigned to this trainer's workout plans
        var uniqueMembersCount = await _context.WorkoutPlans
            .Where(wp => wp.TrainerId == trainer.TrainerId)
            .Select(wp => wp.MemberId)
            .Distinct()
            .CountAsync();

        // Recent workout plans created by this trainer (newest first)
        var recentWorkoutPlans = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Member)
            .Where(wp => wp.TrainerId == trainer.TrainerId)
            .OrderByDescending(wp => wp.CreatedDate)
            .ThenByDescending(wp => wp.WorkoutPlanId)
            .Take(5)
            .ToListAsync();

        var viewModel = new TrainerDashboardViewModel
        {
            TrainerId = trainer.TrainerId,
            TrainerName = trainer.FullName,
            Specialization = trainer.Specialization,
            Email = trainer.Email,
            Phone = trainer.Phone,
            AssignedWorkoutPlansCount = assignedWorkoutPlansCount,
            UniqueAssignedMembersCount = uniqueMembersCount,
            RecentWorkoutPlans = recentWorkoutPlans
        };

        return View(viewModel);
    }
}
