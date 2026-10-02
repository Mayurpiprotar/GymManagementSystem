using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using GymManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

/// <summary>
/// Administrative controller for trainer recommendation matching and final trainer assignments.
/// Strict rules:
/// - Admin ONLY.
/// - NO automatic assignment. Admin reviews recommendations and makes the final decision.
/// - Only canonically Active, Paid memberships with valid Member and Training Goal are eligible.
/// - Only approved, Identity-linked Trainers with active specializations are candidates.
/// - Exact matches prioritized by lowest active workload, then earlier HireDate tie-breaker.
/// - Non-exact assignments require explicit confirmation from the Admin.
/// </summary>
[Authorize(Roles = "Admin")]
public class TrainerAssignmentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TrainerAssignmentController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // =========================================================================
    // 1. ELIGIBLE MEMBERSHIPS OVERVIEW (LIST VIEW)
    // =========================================================================

    /// <summary>
    /// Displays all memberships eligible for trainer assignment, along with top recommendations.
    /// Route: /Admin/TrainerAssignments or /TrainerAssignment
    /// </summary>
    [HttpGet]
    [Route("Admin/TrainerAssignments")]
    [Route("TrainerAssignment")]
    [Route("TrainerAssignment/Index")]
    public async Task<IActionResult> Index(string filter = "all")
    {
        var today = DateTime.Today;

        // Load all memberships with related entities for authoritative status & eligibility calculation
        var allMemberships = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.AssignedTrainer)
            .Include(m => m.Payments)
            .OrderByDescending(m => m.StartDate)
            .ThenByDescending(m => m.MembershipId)
            .ToListAsync();

        // Load candidate trainers once for recommendation preview in list
        var candidateTrainers = await GetApprovedCandidateTrainersAsync();
        var trainerWorkloads = await TrainerWorkloadService.GetActiveTrainerWorkloadsAsync(
            _context, candidateTrainers.Select(t => t.TrainerId), today);

        var eligibleItems = new List<TrainerAssignmentListItemViewModel>();
        var ineligibleItems = new List<IneligibleMembershipInfoViewModel>();

        foreach (var m in allMemberships)
        {
            var canonicalStatus = MembershipStatusResolver.ResolveStatus(m, today);
            var isEligible = MembershipStatusResolver.CanAssignTrainer(m, out string failureReason, today);

            if (isEligible)
            {
                // Calculate recommendation preview for this membership's goal
                TrainerCandidateViewModel? topRecommendation = null;
                if (m.TrainingGoalSpecializationId.HasValue)
                {
                    var ranked = RankTrainersForGoal(candidateTrainers, trainerWorkloads, m.TrainingGoalSpecializationId.Value);
                    topRecommendation = ranked.FirstOrDefault(t => t.IsExactMatch);
                }

                eligibleItems.Add(new TrainerAssignmentListItemViewModel
                {
                    MembershipId = m.MembershipId,
                    MemberId = m.MemberId,
                    MemberName = m.Member?.FullName ?? "Unknown Member",
                    MemberEmail = m.Member?.Email ?? string.Empty,
                    PlanName = m.MembershipPlan?.Name ?? "Membership Plan",
                    StartDate = m.StartDate,
                    EndDate = m.EndDate,
                    CanonicalStatus = canonicalStatus,
                    TrainingGoalSpecializationId = m.TrainingGoalSpecializationId,
                    TrainingGoalName = m.TrainingGoalSpecialization?.Name ?? "General Training",
                    ExperienceLevel = m.ExperienceLevel,
                    TrainingPreference = m.TrainingPreference,
                    AssignedTrainerId = m.AssignedTrainerId,
                    AssignedTrainerName = m.AssignedTrainer?.FullName,
                    RecommendedTrainer = topRecommendation
                });
            }
            else
            {
                ineligibleItems.Add(new IneligibleMembershipInfoViewModel
                {
                    MembershipId = m.MembershipId,
                    MemberName = m.Member?.FullName ?? "Unknown Member",
                    MemberEmail = m.Member?.Email ?? string.Empty,
                    PlanName = m.MembershipPlan?.Name ?? "Membership Plan",
                    StartDate = m.StartDate,
                    EndDate = m.EndDate,
                    CanonicalStatus = canonicalStatus,
                    HasPaidPayment = MembershipStatusResolver.IsPaidMembership(m),
                    HasTrainingGoal = m.TrainingGoalSpecializationId.HasValue && m.TrainingGoalSpecializationId.Value > 0,
                    IneligibilityReason = failureReason
                });
            }
        }

        // Apply filter tab
        var filteredEligible = filter.ToLowerInvariant() switch
        {
            "unassigned" => eligibleItems.Where(e => !e.IsAssigned).ToList(),
            "assigned" => eligibleItems.Where(e => e.IsAssigned).ToList(),
            _ => eligibleItems
        };

        var viewModel = new TrainerAssignmentListViewModel
        {
            EligibleMemberships = filteredEligible,
            IneligibleMemberships = ineligibleItems,
            Filter = filter.ToLowerInvariant(),
            TotalEligibleCount = eligibleItems.Count,
            UnassignedCount = eligibleItems.Count(e => !e.IsAssigned),
            AssignedCount = eligibleItems.Count(e => e.IsAssigned),
            IneligibleCount = ineligibleItems.Count
        };

        return View(viewModel);
    }

    // =========================================================================
    // 2. ASSIGNMENT & MATCHING DETAIL VIEW
    // =========================================================================

    /// <summary>
    /// Displays matching trainers, workload ranking, and top recommendation for an eligible membership.
    /// Route: /Admin/TrainerAssignments/Assign/{id} or /TrainerAssignment/Assign/{id}
    /// </summary>
    [HttpGet]
    [Route("Admin/TrainerAssignments/Assign/{id:int}")]
    [Route("TrainerAssignment/Assign/{id:int}")]
    public async Task<IActionResult> Assign(int id)
    {
        var today = DateTime.Today;

        var membership = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.AssignedTrainer)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        if (membership == null)
        {
            return NotFound();
        }

        // Verify eligibility before displaying matching options
        if (!MembershipStatusResolver.CanAssignTrainer(membership, out string failureReason, today))
        {
            SetErrorMessage($"Membership #{membership.MembershipId} is not eligible for trainer assignment: {failureReason}");
            return RedirectToAction(nameof(Index));
        }

        var candidateTrainers = await GetApprovedCandidateTrainersAsync();
        var trainerWorkloads = await TrainerWorkloadService.GetActiveTrainerWorkloadsAsync(
            _context, candidateTrainers.Select(t => t.TrainerId), today);

        var goalId = membership.TrainingGoalSpecializationId!.Value;
        var rankedTrainers = RankTrainersForGoal(candidateTrainers, trainerWorkloads, goalId);

        var exactMatches = rankedTrainers.Where(t => t.IsExactMatch).ToList();
        var otherApproved = rankedTrainers.Where(t => !t.IsExactMatch).ToList();

        // Recommendation is strictly the top candidate among exact specialization matches
        TrainerCandidateViewModel? recommendedTrainer = null;
        if (exactMatches.Any())
        {
            recommendedTrainer = exactMatches.First();
            recommendedTrainer.IsRecommended = true;
        }

        var viewModel = new TrainerAssignmentDetailViewModel
        {
            MembershipId = membership.MembershipId,
            MemberId = membership.MemberId,
            MemberName = membership.Member?.FullName ?? "Member",
            MemberEmail = membership.Member?.Email ?? string.Empty,
            MemberPhone = membership.Member?.Phone ?? string.Empty,
            PlanName = membership.MembershipPlan?.Name ?? "Membership Plan",
            PlanPrice = membership.MembershipPlan?.Price ?? 0,
            StartDate = membership.StartDate,
            EndDate = membership.EndDate,
            CanonicalStatus = MembershipStatusResolver.ResolveStatus(membership, today),
            IsPaid = MembershipStatusResolver.IsPaidMembership(membership),
            TrainingGoalSpecializationId = goalId,
            TrainingGoalName = membership.TrainingGoalSpecialization?.Name ?? "General Training",
            ExperienceLevel = membership.ExperienceLevel,
            TrainingPreference = membership.TrainingPreference,
            CurrentAssignedTrainerId = membership.AssignedTrainerId,
            CurrentAssignedTrainerName = membership.AssignedTrainer?.FullName,
            RecommendedTrainer = recommendedTrainer,
            MatchingTrainers = exactMatches,
            OtherApprovedTrainers = otherApproved
        };

        return View(viewModel);
    }

    // =========================================================================
    // 3. ADMIN ASSIGNMENT SUBMISSION (POST)
    // =========================================================================

    /// <summary>
    /// Processes the final trainer assignment or reassignment selected by the Admin.
    /// Performs exhaustive server-side validation. Never trusts client-submitted data.
    /// Route: POST /Admin/TrainerAssignments/Assign or /TrainerAssignment/Assign
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Admin/TrainerAssignments/Assign")]
    [Route("TrainerAssignment/Assign")]
    public async Task<IActionResult> Assign(TrainerAssignmentSubmitViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetErrorMessage("Invalid assignment request. Please select a valid trainer.");
            return RedirectToAction(nameof(Assign), new { id = model.MembershipId });
        }

        var today = DateTime.Today;

        // 1. Fresh server-side reload of Membership from database with all relevant relations
        var membership = await _context.Memberships
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == model.MembershipId);

        if (membership == null)
        {
            return NotFound();
        }

        // 2. Server-side validation of membership eligibility
        if (!MembershipStatusResolver.CanAssignTrainer(membership, out string failureReason, today))
        {
            SetErrorMessage($"Assignment rejected: {failureReason}");
            return RedirectToAction(nameof(Index));
        }

        // 3. Fresh server-side reload of Trainer
        var trainer = await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.TrainerSpecializations)
                .ThenInclude(ts => ts.Specialization)
            .FirstOrDefaultAsync(t => t.TrainerId == model.TrainerId);

        if (trainer == null)
        {
            SetErrorMessage("Selected trainer record was not found.");
            return RedirectToAction(nameof(Assign), new { id = model.MembershipId });
        }

        // 4. Verify trainer has linked Identity account
        if (string.IsNullOrEmpty(trainer.UserId))
        {
            SetErrorMessage("Selected trainer does not have an active login account.");
            return RedirectToAction(nameof(Assign), new { id = model.MembershipId });
        }

        // 5. Verify trainer has 'Trainer' Identity role
        var trainerUser = trainer.User ?? await _userManager.FindByIdAsync(trainer.UserId);
        if (trainerUser == null || !await _userManager.IsInRoleAsync(trainerUser, "Trainer"))
        {
            SetErrorMessage("Selected user does not hold an authorized Trainer role.");
            return RedirectToAction(nameof(Assign), new { id = model.MembershipId });
        }

        // 6. Verify trainer has at least one specialization
        if (trainer.TrainerSpecializations == null || !trainer.TrainerSpecializations.Any())
        {
            SetErrorMessage("Selected trainer has no registered specializations.");
            return RedirectToAction(nameof(Assign), new { id = model.MembershipId });
        }

        // 7. Verify exact specialization match vs non-exact manual confirmation
        var isExactMatch = trainer.TrainerSpecializations.Any(ts =>
            ts.SpecializationId == membership.TrainingGoalSpecializationId!.Value);

        if (!isExactMatch && !model.ConfirmNonExact)
        {
            SetErrorMessage($"Trainer '{trainer.FullName}' does not have an exact specialization match for '{membership.TrainingGoalSpecialization?.Name}'. Explicit confirmation is required to assign this trainer.");
            return RedirectToAction(nameof(Assign), new { id = model.MembershipId });
        }

        // 8. Apply assignment / reassignment
        var previousTrainerId = membership.AssignedTrainerId;
        membership.AssignedTrainerId = trainer.TrainerId;

        // Preserve all dates, payments, and training preferences intact
        await _context.SaveChangesAsync();

        if (previousTrainerId.HasValue && previousTrainerId.Value != trainer.TrainerId)
        {
            SetSuccessMessage($"Trainer successfully reassigned to '{trainer.FullName}' for member '{membership.Member?.FullName}'.");
        }
        else
        {
            SetSuccessMessage($"Trainer '{trainer.FullName}' was successfully assigned to member '{membership.Member?.FullName}'.");
        }

        return RedirectToAction(nameof(Index));
    }

    private void SetErrorMessage(string message)
    {
        if (TempData != null)
        {
            TempData["ErrorMessage"] = message;
        }
    }

    private void SetSuccessMessage(string message)
    {
        if (TempData != null)
        {
            TempData["SuccessMessage"] = message;
        }
    }

    // =========================================================================
    // PRIVATE ALGORITHMIC HELPERS
    // =========================================================================

    /// <summary>
    /// Retrieves all approved, Identity-linked Trainers who have at least one specialization.
    /// Pending or rejected applicant records are strictly excluded.
    /// </summary>
    private async Task<List<Trainer>> GetApprovedCandidateTrainersAsync()
    {
        return await _context.Trainers
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.TrainerSpecializations)
                .ThenInclude(ts => ts.Specialization)
            .Where(t => t.IsVerified && !string.IsNullOrEmpty(t.UserId) && t.TrainerSpecializations.Any())
            .ToListAsync();
    }

    /// <summary>
    /// Ranks candidate trainers according to core business requirements:
    /// 1. Exact specialization match (IsExactMatch DESC)
    /// 2. Lowest active workload (ActiveWorkload ASC)
    /// 3. Earlier HireDate tie-breaker (HireDate ASC)
    /// 4. TrainerId deterministic tie-breaker (TrainerId ASC)
    /// </summary>
    private static List<TrainerCandidateViewModel> RankTrainersForGoal(
        IEnumerable<Trainer> candidateTrainers,
        Dictionary<int, int> workloads,
        int trainingGoalSpecializationId)
    {
        var list = new List<TrainerCandidateViewModel>();

        foreach (var t in candidateTrainers)
        {
            var specs = t.TrainerSpecializations
                .Where(ts => ts.Specialization != null)
                .Select(ts => ts.Specialization!.Name)
                .ToList();

            var specIds = t.TrainerSpecializations
                .Select(ts => ts.SpecializationId)
                .ToList();

            var isExact = specIds.Contains(trainingGoalSpecializationId);
            var workload = workloads.TryGetValue(t.TrainerId, out var count) ? count : 0;

            list.Add(new TrainerCandidateViewModel
            {
                TrainerId = t.TrainerId,
                FullName = t.FullName,
                Email = t.Email,
                Phone = t.Phone,
                Specializations = specs,
                SpecializationIds = specIds,
                ActiveWorkload = workload,
                HireDate = t.HireDate,
                IsExactMatch = isExact,
                IsRecommended = false
            });
        }

        // Conceptual ordering: Exact match DESC -> ActiveWorkload ASC -> HireDate ASC -> TrainerId ASC
        return list
            .OrderByDescending(t => t.IsExactMatch)
            .ThenBy(t => t.ActiveWorkload)
            .ThenBy(t => t.HireDate)
            .ThenBy(t => t.TrainerId)
            .ToList();
    }
}
