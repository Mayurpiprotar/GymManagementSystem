using GymManagementSystem.Data;
using GymManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public class MemberManagementController : Controller
{
    private readonly ApplicationDbContext _context;

    public MemberManagementController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: MemberManagement
    public async Task<IActionResult> Index()
    {
        var members = await _context.Members
            .AsNoTracking()
            .OrderByDescending(m => m.JoinDate)
            .ToListAsync();

        return View(members);
    }

    // GET: MemberManagement/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var member = await _context.Members
            .AsNoTracking()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.MemberId == id);

        if (member == null)
        {
            return NotFound();
        }

        return View(member);
    }

    // GET: MemberManagement/Create
    public IActionResult Create()
    {
        var member = new Member
        {
            JoinDate = DateTime.Today,
            DateOfBirth = DateTime.Today.AddYears(-20),
            Gender = "Unspecified"
        };

        return View(member);
    }

    // POST: MemberManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("FullName,Email,Phone,DateOfBirth,Gender,Address,JoinDate")] Member member)
    {
        ModelState.Remove("UserId");
        ModelState.Remove("User");

        if (ModelState.IsValid)
        {
            _context.Members.Add(member);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Member '{member.FullName}' was created successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(member);
    }

    // GET: MemberManagement/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var member = await _context.Members.FindAsync(id);
        if (member == null)
        {
            return NotFound();
        }

        return View(member);
    }

    // POST: MemberManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("MemberId,FullName,Email,Phone,DateOfBirth,Gender,Address,JoinDate")] Member member)
    {
        if (id != member.MemberId)
        {
            return NotFound();
        }

        ModelState.Remove("UserId");
        ModelState.Remove("User");

        if (ModelState.IsValid)
        {
            var existingMember = await _context.Members.FindAsync(id);
            if (existingMember == null)
            {
                return NotFound();
            }

            // Update only manageable fields, preserving internal Identity UserId linkage
            existingMember.FullName = member.FullName;
            existingMember.Email = member.Email;
            existingMember.Phone = member.Phone;
            existingMember.DateOfBirth = member.DateOfBirth;
            existingMember.Gender = member.Gender;
            existingMember.Address = member.Address;
            existingMember.JoinDate = member.JoinDate;

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Member '{existingMember.FullName}' was updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await MemberExistsAsync(member.MemberId))
                {
                    return NotFound();
                }
                throw;
            }
        }

        return View(member);
    }

    // GET: MemberManagement/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var member = await _context.Members
            .AsNoTracking()
            .Include(m => m.Memberships)
            .Include(m => m.Payments)
            .Include(m => m.WorkoutPlans)
            .FirstOrDefaultAsync(m => m.MemberId == id);

        if (member == null)
        {
            return NotFound();
        }

        return View(member);
    }

    // POST: MemberManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var member = await _context.Members
            .Include(m => m.Memberships)
            .Include(m => m.Payments)
            .Include(m => m.WorkoutPlans)
            .FirstOrDefaultAsync(m => m.MemberId == id);

        if (member == null)
        {
            return NotFound();
        }

        // Check for related records due to Restrict foreign key constraints
        if (member.Memberships.Count > 0 || member.Payments.Count > 0 || member.WorkoutPlans.Count > 0)
        {
            TempData["ErrorMessage"] = $"Cannot delete member '{member.FullName}' because they have associated records (Memberships: {member.Memberships.Count}, Payments: {member.Payments.Count}, Workout Plans: {member.WorkoutPlans.Count}). Please remove related records first.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        try
        {
            _context.Members.Remove(member);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Member '{member.FullName}' was deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Unable to delete member due to database constraints. Please check for related records.";
            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    private async Task<bool> MemberExistsAsync(int id)
    {
        return await _context.Members.AnyAsync(e => e.MemberId == id);
    }
}
