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
public class PaymentManagementController : Controller
{
    private readonly ApplicationDbContext _context;

    public static readonly string[] AllowedPaymentMethods = ["Cash", "Card", "UPI", "Bank Transfer"];
    public static readonly string[] AllowedStatuses = ["Paid", "Pending", "Failed", "Refunded"];

    public PaymentManagementController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: PaymentManagement
    public async Task<IActionResult> Index()
    {
        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Member)
            .Include(p => p.Membership)
                .ThenInclude(m => m!.MembershipPlan)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentId)
            .ToListAsync();

        return View(payments);
    }

    // GET: PaymentManagement/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var payment = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Member)
            .Include(p => p.Membership)
                .ThenInclude(m => m!.MembershipPlan)
            .FirstOrDefaultAsync(p => p.PaymentId == id);

        if (payment == null)
        {
            return NotFound();
        }

        return View(payment);
    }

    // GET: PaymentManagement/Create
    public async Task<IActionResult> Create()
    {
        var model = new PaymentViewModel
        {
            PaymentDate = DateTime.Today,
            PaymentMethod = "Cash",
            Status = "Paid"
        };

        await PopulateDropdownListsAsync(model);
        return View(model);
    }

    // POST: PaymentManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentViewModel model)
    {
        ModelState.Remove(nameof(model.MemberList));
        ModelState.Remove(nameof(model.MembershipList));
        ModelState.Remove(nameof(model.PaymentMethodList));
        ModelState.Remove(nameof(model.StatusList));
        ModelState.Remove(nameof(model.AvailableMemberships));

        var memberExists = await _context.Members.AnyAsync(m => m.MemberId == model.MemberId);
        if (!memberExists)
        {
            ModelState.AddModelError(nameof(model.MemberId), "The selected member does not exist.");
        }

        var membership = await _context.Memberships
            .Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.MembershipId == model.MembershipId);

        if (membership == null)
        {
            ModelState.AddModelError(nameof(model.MembershipId), "The selected membership does not exist.");
        }
        else if (memberExists && membership.MemberId != model.MemberId)
        {
            ModelState.AddModelError(nameof(model.MembershipId), "The selected membership does not belong to the selected member.");
        }

        if (!string.IsNullOrEmpty(model.PaymentMethod) && !AllowedPaymentMethods.Contains(model.PaymentMethod))
        {
            ModelState.AddModelError(nameof(model.PaymentMethod), "Invalid payment method selected.");
        }

        if (!string.IsNullOrEmpty(model.Status) && !AllowedStatuses.Contains(model.Status))
        {
            ModelState.AddModelError(nameof(model.Status), "Invalid payment status selected.");
        }

        if (ModelState.IsValid)
        {
            var payment = new Payment
            {
                MemberId = model.MemberId,
                MembershipId = model.MembershipId,
                Amount = model.Amount,
                PaymentDate = model.PaymentDate,
                PaymentMethod = model.PaymentMethod,
                Status = model.Status
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Payment of ${payment.Amount:0.00} recorded successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownListsAsync(model);
        return View(model);
    }

    // GET: PaymentManagement/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var payment = await _context.Payments.FindAsync(id);
        if (payment == null)
        {
            return NotFound();
        }

        var model = new PaymentViewModel
        {
            PaymentId = payment.PaymentId,
            MemberId = payment.MemberId,
            MembershipId = payment.MembershipId,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status
        };

        await PopulateDropdownListsAsync(model);
        return View(model);
    }

    // POST: PaymentManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PaymentViewModel model)
    {
        if (id != model.PaymentId)
        {
            return NotFound();
        }

        ModelState.Remove(nameof(model.MemberList));
        ModelState.Remove(nameof(model.MembershipList));
        ModelState.Remove(nameof(model.PaymentMethodList));
        ModelState.Remove(nameof(model.StatusList));
        ModelState.Remove(nameof(model.AvailableMemberships));

        var payment = await _context.Payments.FindAsync(id);
        if (payment == null)
        {
            return NotFound();
        }

        var memberExists = await _context.Members.AnyAsync(m => m.MemberId == model.MemberId);
        if (!memberExists)
        {
            ModelState.AddModelError(nameof(model.MemberId), "The selected member does not exist.");
        }

        var membership = await _context.Memberships
            .Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.MembershipId == model.MembershipId);

        if (membership == null)
        {
            ModelState.AddModelError(nameof(model.MembershipId), "The selected membership does not exist.");
        }
        else if (memberExists && membership.MemberId != model.MemberId)
        {
            ModelState.AddModelError(nameof(model.MembershipId), "The selected membership does not belong to the selected member.");
        }

        if (!string.IsNullOrEmpty(model.PaymentMethod) && !AllowedPaymentMethods.Contains(model.PaymentMethod))
        {
            ModelState.AddModelError(nameof(model.PaymentMethod), "Invalid payment method selected.");
        }

        if (!string.IsNullOrEmpty(model.Status) && !AllowedStatuses.Contains(model.Status))
        {
            ModelState.AddModelError(nameof(model.Status), "Invalid payment status selected.");
        }

        if (ModelState.IsValid)
        {
            payment.MemberId = model.MemberId;
            payment.MembershipId = model.MembershipId;
            payment.Amount = model.Amount;
            payment.PaymentDate = model.PaymentDate;
            payment.PaymentMethod = model.PaymentMethod;
            payment.Status = model.Status;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Payment #{payment.PaymentId} updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownListsAsync(model);
        return View(model);
    }

    // GET: PaymentManagement/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var payment = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Member)
            .Include(p => p.Membership)
                .ThenInclude(m => m!.MembershipPlan)
            .FirstOrDefaultAsync(p => p.PaymentId == id);

        if (payment == null)
        {
            return NotFound();
        }

        return View(payment);
    }

    // POST: PaymentManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null)
        {
            return NotFound();
        }

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Payment #{id} deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownListsAsync(PaymentViewModel model)
    {
        var members = await _context.Members
            .AsNoTracking()
            .OrderBy(m => m.FullName)
            .ToListAsync();

        var memberships = await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Member)
            .Include(m => m.MembershipPlan)
            .Include(m => m.Payments)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync();

        model.MemberList = members.Select(m => new SelectListItem
        {
            Value = m.MemberId.ToString(),
            Text = $"{m.FullName} ({m.Email})"
        });

        model.MembershipList = memberships.Select(ms => new SelectListItem
        {
            Value = ms.MembershipId.ToString(),
            Text = $"#{ms.MembershipId} - {ms.MembershipPlan?.Name} (${ms.MembershipPlan?.Price:0.00}) - {ms.Member?.FullName}"
        });

        model.AvailableMemberships = memberships.Select(ms => new MembershipSelectItem
        {
            MembershipId = ms.MembershipId,
            MemberId = ms.MemberId,
            MemberName = ms.Member?.FullName ?? string.Empty,
            PlanName = ms.MembershipPlan?.Name ?? string.Empty,
            PlanPrice = ms.MembershipPlan?.Price ?? 0,
            Status = MembershipStatusResolver.ResolveStatus(ms)
        }).ToList();

        model.PaymentMethodList = AllowedPaymentMethods.Select(m => new SelectListItem
        {
            Value = m,
            Text = m
        });

        model.StatusList = AllowedStatuses.Select(s => new SelectListItem
        {
            Value = s,
            Text = s
        });
    }
}
