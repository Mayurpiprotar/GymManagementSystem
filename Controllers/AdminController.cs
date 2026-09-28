using GymManagementSystem.Data;
using GymManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;

        // Summary metric counts
        var totalMembers = await _context.Members.CountAsync();
        var totalTrainers = await _context.Trainers.CountAsync();
        var totalMembershipPlans = await _context.MembershipPlans.CountAsync();

        // Active memberships determined by date range: StartDate <= today AND EndDate >= today
        var activeMemberships = await _context.Memberships
            .CountAsync(m => m.StartDate <= today && m.EndDate >= today);

        var totalPayments = await _context.Payments.CountAsync();
        var totalWorkoutPlans = await _context.WorkoutPlans.CountAsync();

        // Recent members (newest JoinDate first)
        var recentMembers = await _context.Members
            .AsNoTracking()
            .OrderByDescending(m => m.JoinDate)
            .ThenByDescending(m => m.MemberId)
            .Take(5)
            .ToListAsync();

        // Recent payments (newest PaymentDate first)
        var recentPayments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Member)
            .Include(p => p.Membership)
                .ThenInclude(ms => ms!.MembershipPlan)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentId)
            .Take(5)
            .ToListAsync();

        var viewModel = new AdminDashboardViewModel
        {
            TotalMembers = totalMembers,
            TotalTrainers = totalTrainers,
            TotalMembershipPlans = totalMembershipPlans,
            ActiveMemberships = activeMemberships,
            TotalPayments = totalPayments,
            TotalWorkoutPlans = totalWorkoutPlans,
            RecentMembers = recentMembers,
            RecentPayments = recentPayments
        };

        return View(viewModel);
    }
}
