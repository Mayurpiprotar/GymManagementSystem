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

    public MemberController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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
            WorkoutPlans = workoutPlans
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
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.MembershipId == id);

        // Security / Isolation: Membership must exist and belong to current member
        if (membership == null || membership.MemberId != member.MemberId)
        {
            return NotFound("Membership record was not found or access is denied.");
        }

        var canonicalStatus = MembershipStatusResolver.ResolveStatus(membership);

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
            PreviousPayments = membership.Payments.OrderByDescending(p => p.PaymentDate).ToList()
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
            PreviousPayments = membership.Payments.OrderByDescending(p => p.PaymentDate).ToList(),
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

        // Authoritative pricing from database record
        var authoritativeAmount = membership.MembershipPlan.Price;
        var transactionId = $"DEMO-{Guid.NewGuid():N}".ToUpperInvariant();

        if (simulateSuccess)
        {
            // Transactional payment creation and membership activation
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = new Payment
                {
                    MemberId = member.MemberId,
                    MembershipId = membership.MembershipId,
                    Amount = authoritativeAmount,
                    PaymentDate = DateTime.Today,
                    PaymentMethod = GymConstants.PaymentMethods.DemoGateway,
                    Status = GymConstants.PaymentStatuses.Paid,
                    TransactionId = transactionId,
                    Notes = "Demo payment successful (College simulation)"
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

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
}
