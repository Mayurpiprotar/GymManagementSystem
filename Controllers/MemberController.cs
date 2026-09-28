using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

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

    // GET: Member or Member/Index
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var member = await _context.Members
            .AsNoTracking()
            .Include(m => m.Memberships)
                .ThenInclude(ms => ms.MembershipPlan)
            .FirstOrDefaultAsync(m => m.UserId == user.Id);

        if (member == null)
        {
            return View(new MemberDashboardViewModel
            {
                MemberName = user.FullName ?? user.UserName ?? "Member",
                Email = user.Email ?? string.Empty,
                HasMembership = false
            });
        }

        var memberships = member.Memberships
            .OrderByDescending(ms => ms.StartDate)
            .ToList();

        // Calculate real-time status according to date rules
        foreach (var ms in memberships)
        {
            ms.Status = MembershipManagementController.CalculateMembershipStatus(ms.StartDate, ms.EndDate);
        }

        // Current membership is the first Active membership, or the latest Upcoming, or the most recent Expired
        var currentMembership = memberships.FirstOrDefault(ms => ms.Status == "Active")
                             ?? memberships.FirstOrDefault(ms => ms.Status == "Upcoming")
                             ?? memberships.FirstOrDefault();

        var viewModel = new MemberDashboardViewModel
        {
            MemberId = member.MemberId,
            MemberName = member.FullName,
            Email = member.Email,
            Phone = member.Phone,
            JoinDate = member.JoinDate,
            HasMembership = currentMembership != null,
            CurrentMembership = currentMembership,
            AllMemberships = memberships
        };

        return View(viewModel);
    }
}
