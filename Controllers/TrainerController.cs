using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using GymManagementSystem.Services;
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
    private readonly ReferralService _referralService;

    public TrainerController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ReferralService referralService)
    {
        _context = context;
        _userManager = userManager;
        _referralService = referralService;
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
                Email = user.Email ?? string.Empty,
                IsVerified = false
            });
        }

        // If trainer is not verified, show verification status only and lock member lists
        if (!trainer.IsVerified)
        {
            var unverifiedVm = new TrainerDashboardViewModel
            {
                TrainerId = trainer.TrainerId,
                TrainerName = trainer.FullName,
                Specialization = trainer.Specialization,
                Email = trainer.Email,
                Phone = trainer.Phone,
                IsVerified = false,
                AssignedMemberships = new List<Membership>(),
                RecentWorkoutPlans = new List<WorkoutPlan>()
            };
            return View(unverifiedVm);
        }

        var today = DateTime.Today;

        // Load memberships assigned to this trainer (Part O: Trainer sees assigned members)
        var assignedMemberships = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.Payments)
            .Where(m => m.AssignedTrainerId == trainer.TrainerId)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync();

        foreach (var ms in assignedMemberships)
        {
            ms.Status = MembershipStatusResolver.ResolveStatus(ms, today);
        }

        // Section 1: Trainer sees ONLY assigned, active, paid members with valid member relationship
        var eligibleAssignedMemberships = assignedMemberships
            .Where(ms => ms.Member != null
                      && ms.Status == GymConstants.MembershipStatuses.Active
                      && ms.Payments != null && ms.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid))
            .ToList();

        var activeAssignedMembersCount = eligibleAssignedMemberships.Count;

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

        var referralBonus = await _referralService.GetTrainerMonthlyBonusAsync(trainer.TrainerId);

        var viewModel = new TrainerDashboardViewModel
        {
            TrainerId = trainer.TrainerId,
            TrainerName = trainer.FullName,
            Specialization = trainer.Specialization,
            Email = trainer.Email,
            Phone = trainer.Phone,
            IsVerified = true,
            AssignedWorkoutPlansCount = assignedWorkoutPlansCount,
            UniqueAssignedMembersCount = uniqueMembersCount,
            ActiveAssignedMembersCount = activeAssignedMembersCount,
            AssignedMemberships = eligibleAssignedMemberships,
            RecentWorkoutPlans = recentWorkoutPlans,
            ReferralBonus = referralBonus
        };

        return View(viewModel);
    }

    // =========================================================================
    // MEMBER DETAILS (TRAINER-RESTRICTED)
    // =========================================================================

    // GET: Trainer/MemberDetails/5
    public async Task<IActionResult> MemberDetails(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var trainer = await _context.Trainers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == user.Id);

        if (trainer == null || !trainer.IsVerified)
        {
            TempData["ErrorMessage"] = "Your trainer account is awaiting administrative verification before member details can be accessed.";
            return RedirectToAction(nameof(Index));
        }

        var today = DateTime.Today;

        // Verify that this member is assigned to the current trainer and is Active & Paid
        var memberships = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.Payments)
            .Where(m => m.MemberId == id.Value && m.AssignedTrainerId == trainer.TrainerId)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync();

        foreach (var ms in memberships)
        {
            ms.Status = MembershipStatusResolver.ResolveStatus(ms, today);
        }

        var activeMembership = memberships.FirstOrDefault(m =>
            m.Status == GymConstants.MembershipStatuses.Active
            && m.Payments != null && m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid));

        // Return NotFound rather than exposing data or leaking whether the member exists
        if (activeMembership == null || activeMembership.Member == null)
        {
            return NotFound();
        }

        // Load workout plans for this member created by this trainer
        var workoutPlans = await _context.WorkoutPlans
            .AsNoTracking()
            .Where(wp => wp.MemberId == id.Value && wp.TrainerId == trainer.TrainerId)
            .OrderByDescending(wp => wp.CreatedDate)
            .ThenByDescending(wp => wp.WorkoutPlanId)
            .ToListAsync();

        var viewModel = new TrainerMemberDetailsViewModel
        {
            Member = activeMembership.Member,
            CurrentMembership = activeMembership,
            WorkoutPlans = workoutPlans
        };

        return View(viewModel);
    }
}
