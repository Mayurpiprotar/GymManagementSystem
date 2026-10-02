namespace GymManagementSystem.Controllers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using GymManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Member")]
public class MemberController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ReferralService _referralService;

    public MemberController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ReferralService referralService)
    {
        _context = context;
        _userManager = userManager;
        _referralService = referralService;
    }

    // =========================================================================
    // MEMBER DASHBOARD
    // =========================================================================

    // GET: Member or Member/Index
    public async Task<IActionResult> Index()
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            var user = await _userManager.GetUserAsync(User);
            return View(new MemberDashboardViewModel
            {
                MemberName = user?.FullName ?? user?.UserName ?? "Member",
                Email = user?.Email ?? string.Empty,
                HasMembership = false
            });
        }

        var memberships = member.Memberships
            .OrderByDescending(ms => ms.StartDate)
            .ThenByDescending(ms => ms.MembershipId)
            .ToList();

        // Calculate real-time canonical status using authoritative MembershipStatusResolver
        foreach (var ms in memberships)
        {
            ms.Status = MembershipStatusResolver.ResolveStatus(ms);
        }

        // Current membership preference: Active -> Upcoming -> PendingPayment -> most recent
        var currentMembership = memberships.FirstOrDefault(ms => ms.Status == GymConstants.MembershipStatuses.Active)
                             ?? memberships.FirstOrDefault(ms => ms.Status == GymConstants.MembershipStatuses.Upcoming)
                             ?? memberships.FirstOrDefault(ms => ms.Status == GymConstants.MembershipStatuses.PendingPayment)
                             ?? memberships.FirstOrDefault();

        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Membership)
                .ThenInclude(ms => ms!.MembershipPlan)
            .Where(p => p.MemberId == member.MemberId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentId)
            .ToListAsync();

        var workoutPlans = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Trainer)
            .Where(wp => wp.MemberId == member.MemberId)
            .OrderByDescending(wp => wp.CreatedDate)
            .ThenByDescending(wp => wp.WorkoutPlanId)
            .ToListAsync();

        var referralInfo = await _referralService.GetMemberSummaryAsync(member.MemberId);

        var viewModel = new MemberDashboardViewModel
        {
            MemberId = member.MemberId,
            MemberName = member.FullName,
            Email = member.Email,
            Phone = member.Phone,
            JoinDate = member.JoinDate,
            HasMembership = currentMembership != null,
            CurrentMembership = currentMembership,
            AllMemberships = memberships,
            PaymentHistory = payments,
            WorkoutPlans = workoutPlans,
            ReferralInfo = referralInfo
        };

        return View(viewModel);
    }

    // =========================================================================
    // MEMBERSHIP PLAN BROWSING
    // =========================================================================

    // GET: Member/Plans (Allows public/anonymous browsing; purchase is member-only)
    [AllowAnonymous]
    [HttpGet]
    [Route("Member/Plans")]
    [Route("Plans")]
    public async Task<IActionResult> Plans()
    {
        var plans = await _context.MembershipPlans
            .AsNoTracking()
            .Where(p => p.DurationInMonths > 0 && p.Price >= 0)
            .OrderBy(p => p.DurationInMonths)
            .ThenBy(p => p.Price)
            .ToListAsync();

        bool hasActiveOrUpcoming = false;
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Member"))
        {
            var member = await GetCurrentMemberAsync();
            if (member != null)
            {
                hasActiveOrUpcoming = member.Memberships.Any(m =>
                {
                    var status = MembershipStatusResolver.ResolveStatus(m);
                    return status == GymConstants.MembershipStatuses.Active
                        || status == GymConstants.MembershipStatuses.Upcoming;
                });
            }
        }

        ViewData["HasActiveOrUpcoming"] = hasActiveOrUpcoming;
        return View(plans);
    }

    // =========================================================================
    // MEMBERSHIP PURCHASE FLOW
    // =========================================================================

    // GET: Member/Purchase/5
    [HttpGet]
    public async Task<IActionResult> Purchase(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        // Rule: Prevent duplicate active/upcoming purchase
        if (HasActiveOrUpcomingMembership(member))
        {
            TempData["ErrorMessage"] = "You already have an active or upcoming membership. Plan renewal will be available through the renewal workflow.";
            return RedirectToAction(nameof(Plans));
        }

        // Rule: If an unfinalized pending checkout exists for this plan, resume it
        var existingPending = member.Memberships.FirstOrDefault(m =>
            m.MembershipPlanId == id &&
            MembershipStatusResolver.ResolveStatus(m) == GymConstants.MembershipStatuses.PendingPayment);

        if (existingPending != null)
        {
            TempData["InfoMessage"] = "Resuming your pending checkout for this membership plan.";
            return RedirectToAction(nameof(Checkout), new { id = existingPending.MembershipId });
        }

        var plan = await _context.MembershipPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipPlanId == id);

        if (plan == null)
        {
            return NotFound("Selected membership plan was not found.");
        }

        var model = new MembershipPurchaseViewModel
        {
            MembershipPlanId = plan.MembershipPlanId,
            PlanName = plan.Name,
            PlanDurationInMonths = plan.DurationInMonths,
            PlanPrice = plan.Price,
            PlanDescription = plan.Description
        };

        await PopulatePurchaseDropdownsAsync(model);
        return View(model);
    }

    // POST: Member/Purchase
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purchase(MembershipPurchaseViewModel model)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        // Rule: Duplicate purchase lock
        if (HasActiveOrUpcomingMembership(member))
        {
            TempData["ErrorMessage"] = "You already have an active or upcoming membership. Plan renewal will be available through the renewal workflow.";
            return RedirectToAction(nameof(Plans));
        }

        // 1. Authoritative plan lookup from database (client-submitted price is ignored)
        var plan = await _context.MembershipPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipPlanId == model.MembershipPlanId);

        if (plan == null)
        {
            ModelState.AddModelError(nameof(model.MembershipPlanId), "The selected membership plan does not exist.");
        }

        // 2. Training goal specialization verification
        var goalExists = await _context.Specializations
            .AsNoTracking()
            .AnyAsync(s => s.SpecializationId == model.TrainingGoalSpecializationId);

        if (!goalExists)
        {
            ModelState.AddModelError(nameof(model.TrainingGoalSpecializationId), "Please select a valid training goal specialization.");
        }

        // 3. Experience level verification
        if (!GymConstants.ExperienceLevels.All.Contains(model.ExperienceLevel))
        {
            ModelState.AddModelError(nameof(model.ExperienceLevel), "Please select a valid fitness experience level.");
        }

        if (!ModelState.IsValid)
        {
            if (plan != null)
            {
                model.PlanName = plan.Name;
                model.PlanDurationInMonths = plan.DurationInMonths;
                model.PlanPrice = plan.Price;
                model.PlanDescription = plan.Description;
            }
            await PopulatePurchaseDropdownsAsync(model);
            return View(model);
        }

        // 4. Calculate Server-Controlled Dates
        var startDate = DateTime.Today.Date;
        var endDate = startDate.AddMonths(plan!.DurationInMonths).Date;

        // Check if an existing PendingPayment membership for this plan exists to avoid unlimited duplicates
        var existingPending = await _context.Memberships
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MemberId == member.MemberId
                                   && m.MembershipPlanId == plan.MembershipPlanId
                                   && !m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid));

        int targetMembershipId;
        if (existingPending != null)
        {
            // Reuse and refresh existing pending purchase record
            existingPending.TrainingGoalSpecializationId = model.TrainingGoalSpecializationId;
            existingPending.ExperienceLevel = model.ExperienceLevel;
            existingPending.TrainingPreference = model.TrainingPreference.Trim();
            existingPending.StartDate = startDate;
            existingPending.EndDate = endDate;
            existingPending.Status = GymConstants.MembershipStatuses.PendingPayment;

            await _context.SaveChangesAsync();
            targetMembershipId = existingPending.MembershipId;
        }
        else
        {
            // Create new Membership record in PendingPayment state
            var membership = new Membership
            {
                MemberId = member.MemberId,
                MembershipPlanId = plan.MembershipPlanId,
                StartDate = startDate,
                EndDate = endDate,
                Status = GymConstants.MembershipStatuses.PendingPayment,
                TrainingGoalSpecializationId = model.TrainingGoalSpecializationId,
                ExperienceLevel = model.ExperienceLevel,
                TrainingPreference = model.TrainingPreference.Trim()
            };

            _context.Memberships.Add(membership);
            await _context.SaveChangesAsync();
            targetMembershipId = membership.MembershipId;
        }

        return RedirectToAction(nameof(Checkout), new { id = targetMembershipId });
    }

    // =========================================================================
    // MEMBERSHIP RENEWAL WORKFLOW (PHASE 7)
    // =========================================================================

    // GET: Member/Renew/5
    [HttpGet]
    public async Task<IActionResult> Renew(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        var membership = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.AssignedTrainer)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        // Security / Isolation: Membership must belong to current member
        if (membership == null || membership.MemberId != member.MemberId)
        {
            return NotFound("Membership record was not found or access is denied.");
        }

        var today = DateTime.Today;
        var canonicalStatus = MembershipStatusResolver.ResolveStatus(membership, today);

        // Rule: Do NOT allow renewal of PendingPayment membership
        if (canonicalStatus == GymConstants.MembershipStatuses.PendingPayment)
        {
            TempData["InfoMessage"] = "This membership is pending payment. Complete your existing payment instead of renewing.";
            return RedirectToAction(nameof(Checkout), new { id = membership.MembershipId });
        }

        // Rule: If member already has an uncompleted pending checkout, redirect to complete it
        var existingPending = member.Memberships.FirstOrDefault(m =>
            MembershipStatusResolver.ResolveStatus(m, today) == GymConstants.MembershipStatuses.PendingPayment);
        if (existingPending != null)
        {
            TempData["InfoMessage"] = "You have an uncompleted pending checkout. Please complete or review your payment before starting another renewal.";
            return RedirectToAction(nameof(Checkout), new { id = existingPending.MembershipId });
        }

        // Calculate Start Date (Part G):
        // If Active or Upcoming: New StartDate = OldMembership.EndDate.AddDays(1)
        // If Expired: New StartDate = today
        var startDate = (canonicalStatus == GymConstants.MembershipStatuses.Active || canonicalStatus == GymConstants.MembershipStatuses.Upcoming)
            ? membership.EndDate.Date.AddDays(1)
            : today;

        var plan = membership.MembershipPlan;
        var duration = plan?.DurationInMonths ?? 1;
        var endDate = startDate.AddMonths(duration);

        // Verify preserved trainer (Part J):
        int? preservedTrainerId = null;
        string? preservedTrainerName = null;
        string? preservedTrainerEmail = null;
        if (membership.AssignedTrainerId.HasValue)
        {
            var trainer = await _context.Trainers
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TrainerId == membership.AssignedTrainerId.Value);

            if (trainer != null)
            {
                preservedTrainerId = trainer.TrainerId;
                preservedTrainerName = trainer.FullName;
                preservedTrainerEmail = trainer.Email;
            }
        }

        var model = new MembershipRenewalViewModel
        {
            OriginalMembershipId = membership.MembershipId,
            MemberName = member.FullName,
            MemberEmail = member.Email,
            OriginalPlanName = plan?.Name ?? "Membership Plan",
            OriginalStartDate = membership.StartDate,
            OriginalEndDate = membership.EndDate,
            OriginalCanonicalStatus = canonicalStatus,
            PreservedTrainerId = preservedTrainerId,
            PreservedTrainerName = preservedTrainerName,
            PreservedTrainerEmail = preservedTrainerEmail,
            SelectedPlanId = membership.MembershipPlanId,
            TrainingGoalSpecializationId = membership.TrainingGoalSpecializationId ?? 1,
            ExperienceLevel = membership.ExperienceLevel ?? GymConstants.ExperienceLevels.Beginner,
            TrainingPreference = membership.TrainingPreference,
            CalculatedStartDate = startDate,
            CalculatedEndDate = endDate,
            PlanPrice = plan?.Price ?? 0,
            PlanDurationInMonths = duration
        };

        await PopulateRenewalDropdownsAsync(model);
        return View(model);
    }

    // POST: Member/Renew
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(MembershipRenewalViewModel model)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        // 1. Reload original membership from database with related entities
        var originalMembership = await _context.Memberships
            .Include(m => m.MembershipPlan)
            .Include(m => m.AssignedTrainer)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == model.OriginalMembershipId);

        if (originalMembership == null || originalMembership.MemberId != member.MemberId)
        {
            return NotFound("Original membership record was not found or access is denied.");
        }

        var today = DateTime.Today;
        var canonicalStatus = MembershipStatusResolver.ResolveStatus(originalMembership, today);

        // Rule: PendingPayment membership cannot be renewed
        if (canonicalStatus == GymConstants.MembershipStatuses.PendingPayment)
        {
            TempData["InfoMessage"] = "This membership is pending payment. Complete your existing payment instead of renewing.";
            return RedirectToAction(nameof(Checkout), new { id = originalMembership.MembershipId });
        }

        // 2. Authoritative plan lookup from database
        var selectedPlan = await _context.MembershipPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipPlanId == model.SelectedPlanId);

        if (selectedPlan == null)
        {
            ModelState.AddModelError(nameof(model.SelectedPlanId), "The selected membership plan does not exist.");
        }

        // 3. Training goal specialization verification
        var goalExists = await _context.Specializations
            .AsNoTracking()
            .AnyAsync(s => s.SpecializationId == model.TrainingGoalSpecializationId);

        if (!goalExists)
        {
            ModelState.AddModelError(nameof(model.TrainingGoalSpecializationId), "Please select a valid training goal specialization.");
        }

        // 4. Experience level verification
        if (!GymConstants.ExperienceLevels.All.Contains(model.ExperienceLevel))
        {
            ModelState.AddModelError(nameof(model.ExperienceLevel), "Please select a valid fitness experience level.");
        }

        if (!ModelState.IsValid)
        {
            model.OriginalPlanName = originalMembership.MembershipPlan?.Name ?? "Membership Plan";
            model.OriginalStartDate = originalMembership.StartDate;
            model.OriginalEndDate = originalMembership.EndDate;
            model.OriginalCanonicalStatus = canonicalStatus;
            model.PreservedTrainerId = originalMembership.AssignedTrainerId;
            model.PreservedTrainerName = originalMembership.AssignedTrainer?.FullName;

            if (selectedPlan != null)
            {
                var calcStart = (canonicalStatus == GymConstants.MembershipStatuses.Active || canonicalStatus == GymConstants.MembershipStatuses.Upcoming)
                    ? originalMembership.EndDate.Date.AddDays(1)
                    : today;
                model.CalculatedStartDate = calcStart;
                model.CalculatedEndDate = calcStart.AddMonths(selectedPlan.DurationInMonths).Date;
                model.PlanPrice = selectedPlan.Price;
                model.PlanDurationInMonths = selectedPlan.DurationInMonths;
            }

            await PopulateRenewalDropdownsAsync(model);
            return View(model);
        }

        // 5. Calculate Server-Side Dates (Part G & H)
        var newStartDate = (canonicalStatus == GymConstants.MembershipStatuses.Active || canonicalStatus == GymConstants.MembershipStatuses.Upcoming)
            ? originalMembership.EndDate.Date.AddDays(1)
            : today;
        var newEndDate = newStartDate.AddMonths(selectedPlan!.DurationInMonths).Date;

        // 6. Verify and preserve trainer (Part I & J)
        int? preservedTrainerId = null;
        if (originalMembership.AssignedTrainerId.HasValue)
        {
            var trainerExists = await _context.Trainers
                .AnyAsync(t => t.TrainerId == originalMembership.AssignedTrainerId.Value);

            if (trainerExists)
            {
                preservedTrainerId = originalMembership.AssignedTrainerId.Value;
            }
        }

        // 7. Create NEW Membership record (Part K: Status = PendingPayment)
        var newMembership = new Membership
        {
            MemberId = member.MemberId,
            MembershipPlanId = selectedPlan.MembershipPlanId,
            StartDate = newStartDate,
            EndDate = newEndDate,
            Status = GymConstants.MembershipStatuses.PendingPayment,
            TrainingGoalSpecializationId = model.TrainingGoalSpecializationId,
            ExperienceLevel = model.ExperienceLevel,
            TrainingPreference = model.TrainingPreference?.Trim(),
            AssignedTrainerId = preservedTrainerId
        };

        _context.Memberships.Add(newMembership);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Renewal subscription created. Please review and complete your payment.";
        return RedirectToAction(nameof(Checkout), new { id = newMembership.MembershipId });
    }

    // =========================================================================
    // CHECKOUT REVIEW SCREEN
    // =========================================================================

    // GET: Member/Checkout/5
    [HttpGet]
    public async Task<IActionResult> Checkout(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        var membership = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.AssignedTrainer)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        // Security / Isolation: Membership must exist and belong to current member
        if (membership == null || membership.MemberId != member.MemberId)
        {
            return NotFound("Membership record was not found or access is denied.");
        }

        var canonicalStatus = MembershipStatusResolver.ResolveStatus(membership);

        bool isRenewal = await _context.Memberships.AnyAsync(m => m.MemberId == member.MemberId && m.MembershipId != membership.MembershipId && m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid));
        var discountPct = await _referralService.CalculateDiscountPercentAsync(member.MemberId, isRenewal);
        string? discountReason = null;
        if (discountPct > 0)
        {
            discountReason = isRenewal
                ? $"Member Referral Reward: {discountPct}% OFF renewal applied!"
                : $"Welcome Referral Gift: {discountPct}% OFF first membership applied!";
        }

        var viewModel = new MembershipCheckoutViewModel
        {
            MembershipId = membership.MembershipId,
            MemberId = member.MemberId,
            MemberName = member.FullName,
            MemberEmail = member.Email,
            MembershipPlanId = membership.MembershipPlanId,
            PlanName = membership.MembershipPlan?.Name ?? "Membership Plan",
            PlanDurationInMonths = membership.MembershipPlan?.DurationInMonths ?? 1,
            PlanPrice = membership.MembershipPlan?.Price ?? 0,
            PlanDescription = membership.MembershipPlan?.Description,
            TrainingGoalName = membership.TrainingGoalSpecialization?.Name ?? "General Fitness",
            ExperienceLevel = membership.ExperienceLevel ?? "Beginner",
            TrainingPreference = membership.TrainingPreference ?? "General Workout",
            StartDate = membership.StartDate,
            EndDate = membership.EndDate,
            CanonicalStatus = canonicalStatus,
            AssignedTrainerId = membership.AssignedTrainerId,
            AssignedTrainerName = membership.AssignedTrainer?.FullName,
            PreviousPayments = membership.Payments.OrderByDescending(p => p.PaymentDate).ToList(),
            DiscountPercent = discountPct,
            DiscountReason = discountReason
        };

        return View(viewModel);
    }

    // =========================================================================
    // DEMO PAYMENT GATEWAY & SIMULATION
    // =========================================================================

    // GET: Member/DemoPayment/5
    [HttpGet]
    public async Task<IActionResult> DemoPayment(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        var membership = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.AssignedTrainer)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        // Security / Isolation Check
        if (membership == null || membership.MemberId != member.MemberId)
        {
            return NotFound("Membership record was not found or access is denied.");
        }

        // Prevent double payment on already paid membership
        if (MembershipStatusResolver.IsPaidMembership(membership))
        {
            TempData["InfoMessage"] = "This membership subscription has already been paid and activated.";
            return RedirectToAction(nameof(Index));
        }

        bool isRenewal = await _context.Memberships.AnyAsync(m => m.MemberId == member.MemberId && m.MembershipId != membership.MembershipId && m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid));
        var discountPct = await _referralService.CalculateDiscountPercentAsync(member.MemberId, isRenewal);
        string? discountReason = null;
        if (discountPct > 0)
        {
            discountReason = isRenewal
                ? $"Member Referral Reward: {discountPct}% OFF renewal applied!"
                : $"Welcome Referral Gift: {discountPct}% OFF first membership applied!";
        }

        var viewModel = new MembershipCheckoutViewModel
        {
            MembershipId = membership.MembershipId,
            MemberId = member.MemberId,
            MemberName = member.FullName,
            MemberEmail = member.Email,
            MembershipPlanId = membership.MembershipPlanId,
            PlanName = membership.MembershipPlan?.Name ?? "Membership Plan",
            PlanDurationInMonths = membership.MembershipPlan?.DurationInMonths ?? 1,
            PlanPrice = membership.MembershipPlan?.Price ?? 0,
            PlanDescription = membership.MembershipPlan?.Description,
            TrainingGoalName = membership.TrainingGoalSpecialization?.Name ?? "General Fitness",
            ExperienceLevel = membership.ExperienceLevel ?? "Beginner",
            TrainingPreference = membership.TrainingPreference ?? "General Workout",
            StartDate = membership.StartDate,
            EndDate = membership.EndDate,
            CanonicalStatus = MembershipStatusResolver.ResolveStatus(membership),
            AssignedTrainerId = membership.AssignedTrainerId,
            AssignedTrainerName = membership.AssignedTrainer?.FullName,
            PreviousPayments = membership.Payments.OrderByDescending(p => p.PaymentDate).ToList(),
            DiscountPercent = discountPct,
            DiscountReason = discountReason,
            SimulateSuccess = true
        };

        return View(viewModel);
    }

    // POST: Member/ProcessDemoPayment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessDemoPayment(int membershipId, bool simulateSuccess)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        var membership = await _context.Memberships
            .Include(m => m.MembershipPlan)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == membershipId);

        // Security / Isolation Check
        if (membership == null || membership.MemberId != member.MemberId)
        {
            return NotFound("Membership record was not found or access is denied.");
        }

        if (membership.MembershipPlan == null)
        {
            return BadRequest("Associated membership plan data is missing.");
        }

        // Check if already paid
        if (MembershipStatusResolver.IsPaidMembership(membership))
        {
            TempData["InfoMessage"] = "This membership is already paid and in good standing.";
            return RedirectToAction(nameof(Index));
        }

        // Authoritative pricing from database record with referral/renewal discounts
        bool isRenewal = await _context.Memberships.AnyAsync(m => m.MemberId == member.MemberId && m.MembershipId != membership.MembershipId && m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid));
        var discountPct = await _referralService.CalculateDiscountPercentAsync(member.MemberId, isRenewal);
        var basePrice = membership.MembershipPlan.Price;
        var discountAmount = Math.Round(basePrice * (discountPct / 100m), 2);
        var authoritativeAmount = Math.Max(0, basePrice - discountAmount);

        var transactionId = $"DEMO-{Guid.NewGuid():N}".ToUpperInvariant();

        if (simulateSuccess)
        {
            // Transactional payment creation and membership activation
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var notes = discountPct > 0
                    ? $"Demo payment successful (₹{discountAmount:0.00} referral discount applied)"
                    : "Demo payment successful (College simulation)";

                var payment = new Payment
                {
                    MemberId = member.MemberId,
                    MembershipId = membership.MembershipId,
                    Amount = authoritativeAmount,
                    PaymentDate = DateTime.Today,
                    PaymentMethod = GymConstants.PaymentMethods.DemoGateway,
                    Status = GymConstants.PaymentStatuses.Paid,
                    TransactionId = transactionId,
                    Notes = notes
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // If welcome discount was used, mark referral as claimed
                if (!isRenewal && discountPct > 0)
                {
                    var referral = await _context.Referrals.FirstOrDefaultAsync(r => r.ReferredMemberId == member.MemberId && !r.RewardClaimed);
                    if (referral != null)
                    {
                        referral.RewardClaimed = true;
                        referral.Status = "Completed";
                        await _context.SaveChangesAsync();
                    }
                }

                // Recalculate canonical status dynamically based on dates and verified payment
                var canonicalStatus = MembershipStatusResolver.ResolveStatus(membership.StartDate, membership.EndDate, isPaid: true);
                membership.Status = canonicalStatus;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["SuccessMessage"] = $"Payment successful! Transaction ID: {transactionId}. Your membership is now {canonicalStatus}.";
                return RedirectToAction(nameof(PaymentSuccess), new { id = payment.PaymentId });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = $"A database error occurred while processing payment: {ex.Message}";
                return RedirectToAction(nameof(Checkout), new { id = membership.MembershipId });
            }
        }
        else
        {
            // Simulated Failed Payment: preserves failed payment in audit trail, membership remains PendingPayment
            var payment = new Payment
            {
                MemberId = member.MemberId,
                MembershipId = membership.MembershipId,
                Amount = authoritativeAmount,
                PaymentDate = DateTime.Today,
                PaymentMethod = GymConstants.PaymentMethods.DemoGateway,
                Status = GymConstants.PaymentStatuses.Failed,
                TransactionId = transactionId,
                Notes = "Demo payment simulated failure"
            };

            _context.Payments.Add(payment);
            membership.Status = GymConstants.MembershipStatuses.PendingPayment;

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "Payment failed. Your membership has not been activated. You may retry payment when ready.";
            return RedirectToAction(nameof(Checkout), new { id = membership.MembershipId });
        }
    }

    // GET: Member/PaymentSuccess/5
    [HttpGet]
    public async Task<IActionResult> PaymentSuccess(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        var payment = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Membership)
                .ThenInclude(ms => ms!.MembershipPlan)
            .Include(p => p.Membership)
                .ThenInclude(ms => ms!.TrainingGoalSpecialization)
            .FirstOrDefaultAsync(p => p.PaymentId == id);

        if (payment == null || payment.MemberId != member.MemberId)
        {
            return NotFound("Payment receipt was not found or access is denied.");
        }

        return View(payment);
    }

    // GET: Member/DownloadReceipt/5
    [HttpGet]
    [Route("Member/DownloadReceipt/{id:int}")]
    public async Task<IActionResult> DownloadReceipt(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        var payment = await _context.Payments
            .Include(p => p.Member)
            .Include(p => p.Membership)
                .ThenInclude(ms => ms!.MembershipPlan)
            .FirstOrDefaultAsync(p => p.PaymentId == id && p.MemberId == member.MemberId);

        if (payment == null)
        {
            return NotFound("Payment receipt not found.");
        }

        var pdfBytes = PdfReceiptService.GenerateReceiptPdf(payment);
        var fileName = $"IronPulse-Receipt-TXN{payment.PaymentId:D6}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    // =========================================================================
    // MEMBER PAYMENT & MEMBERSHIP HISTORY (ISOLATED TO CURRENT LOGGED-IN MEMBER)
    // =========================================================================

    // GET: Member/Payments
    [HttpGet]
    public async Task<IActionResult> Payments()
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Membership)
                .ThenInclude(ms => ms!.MembershipPlan)
            .Where(p => p.MemberId == member.MemberId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentId)
            .ToListAsync();

        return View(payments);
    }

    // GET: Member/Memberships
    [HttpGet]
    public async Task<IActionResult> Memberships()
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        var memberships = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.MembershipPlan)
            .Include(m => m.TrainingGoalSpecialization)
            .Include(m => m.AssignedTrainer)
                .ThenInclude(t => t!.TrainerSpecializations)
                    .ThenInclude(ts => ts.Specialization)
            .Include(m => m.Payments)
            .Where(m => m.MemberId == member.MemberId)
            .OrderByDescending(m => m.StartDate)
            .ThenByDescending(m => m.MembershipId)
            .ToListAsync();

        // Calculate canonical statuses
        foreach (var ms in memberships)
        {
            ms.Status = MembershipStatusResolver.ResolveStatus(ms);
        }

        return View(memberships);
    }

    // =========================================================================
    // WORKOUT PLAN ISOLATION (MEMBER-ONLY)
    // =========================================================================

    // GET: Member/WorkoutPlanDetails/5
    [HttpGet]
    public async Task<IActionResult> WorkoutPlanDetails(int id)
    {
        var member = await GetCurrentMemberAsync();
        if (member == null)
        {
            return Challenge();
        }

        // Section 4: Member sees ONLY their own workout plans (WorkoutPlan.MemberId == current logged-in Member.MemberId)
        var plan = await _context.WorkoutPlans
            .AsNoTracking()
            .Include(wp => wp.Trainer)
                .ThenInclude(t => t!.TrainerSpecializations)
                    .ThenInclude(ts => ts.Specialization)
            .FirstOrDefaultAsync(wp => wp.WorkoutPlanId == id && wp.MemberId == member.MemberId);

        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private async Task<Member?> GetCurrentMemberAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return null;
        }

        return await _context.Members
            .Include(m => m.Memberships)
                .ThenInclude(ms => ms.MembershipPlan)
            .Include(m => m.Memberships)
                .ThenInclude(ms => ms.TrainingGoalSpecialization)
            .Include(m => m.Memberships)
                .ThenInclude(ms => ms.AssignedTrainer)
                    .ThenInclude(t => t!.TrainerSpecializations)
                        .ThenInclude(ts => ts.Specialization)
            .Include(m => m.Memberships)
                .ThenInclude(ms => ms.Payments)
            .FirstOrDefaultAsync(m => m.UserId == user.Id);
    }

    private static bool HasActiveOrUpcomingMembership(Member member)
    {
        if (member.Memberships == null)
        {
            return false;
        }

        return member.Memberships.Any(m =>
        {
            var status = MembershipStatusResolver.ResolveStatus(m);
            return status == GymConstants.MembershipStatuses.Active
                || status == GymConstants.MembershipStatuses.Upcoming;
        });
    }

    private async Task PopulatePurchaseDropdownsAsync(MembershipPurchaseViewModel model)
    {
        var goals = await _context.Specializations
            .AsNoTracking()
            .OrderBy(s => s.SpecializationId)
            .ToListAsync();

        model.AvailableGoals = goals.Select(g => new SelectListItem
        {
            Value = g.SpecializationId.ToString(),
            Text = $"{g.Name} — {g.Description}"
        });

        model.AvailableExperienceLevels = GymConstants.ExperienceLevels.All.Select(lvl => new SelectListItem
        {
            Value = lvl,
            Text = lvl
        });
    }

    private async Task PopulateRenewalDropdownsAsync(MembershipRenewalViewModel model)
    {
        var plans = await _context.MembershipPlans
            .AsNoTracking()
            .Where(p => p.DurationInMonths > 0 && p.Price >= 0)
            .OrderBy(p => p.DurationInMonths)
            .ToListAsync();

        model.AvailablePlans = plans.Select(p => new SelectListItem
        {
            Value = p.MembershipPlanId.ToString(),
            Text = $"{p.Name} — ₹{p.Price:0.00} ({p.DurationInMonths} {(p.DurationInMonths == 1 ? "month" : "months")})"
        });

        var goals = await _context.Specializations
            .AsNoTracking()
            .OrderBy(s => s.SpecializationId)
            .ToListAsync();

        model.AvailableGoals = goals.Select(g => new SelectListItem
        {
            Value = g.SpecializationId.ToString(),
            Text = $"{g.Name} — {g.Description}"
        });

        model.AvailableExperienceLevels = GymConstants.ExperienceLevels.All.Select(lvl => new SelectListItem
        {
            Value = lvl,
            Text = lvl
        });
    }
}
