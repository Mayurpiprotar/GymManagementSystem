using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public class TrainerManagementController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TrainerManagementController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: TrainerManagement
    public async Task<IActionResult> Index()
    {
        var trainers = await _context.Trainers
            .AsNoTracking()
            .Include(t => t.User)
            .OrderByDescending(t => t.HireDate)
            .ToListAsync();

        return View(trainers);
    }

    // GET: TrainerManagement/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var trainer = await _context.Trainers
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.WorkoutPlans)
            .FirstOrDefaultAsync(t => t.TrainerId == id);

        if (trainer == null)
        {
            return NotFound();
        }

        return View(trainer);
    }

    // GET: TrainerManagement/Create
    public IActionResult Create()
    {
        return View(new TrainerCreateViewModel
        {
            HireDate = DateTime.Today
        });
    }

    // POST: TrainerManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TrainerCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Check if email already used by an Identity account
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        // Check if email already used by a Trainer record
        if (await _context.Trainers.AnyAsync(t => t.Email == model.Email))
        {
            ModelState.AddModelError("Email", "A trainer with this email address already exists.");
            return View(model);
        }

        // 1. Create Identity User
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName
        };

        var userResult = await _userManager.CreateAsync(user, model.Password);
        if (!userResult.Succeeded)
        {
            foreach (var error in userResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // 2. Assign Trainer role
        var roleResult = await _userManager.AddToRoleAsync(user, "Trainer");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // 3. Create Trainer domain record
        var trainer = new Trainer
        {
            FullName = model.FullName,
            Email = model.Email,
            Phone = model.Phone,
            Specialization = model.Specialization,
            HireDate = model.HireDate,
            UserId = user.Id
        };

        try
        {
            _context.Trainers.Add(trainer);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Trainer '{trainer.FullName}' was created successfully with an active login account.";
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            // Atomicity/cleanup: remove created Identity user to avoid orphaned accounts
            await _userManager.DeleteAsync(user);
            throw;
        }
    }

    // GET: TrainerManagement/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var trainer = await _context.Trainers.FindAsync(id);
        if (trainer == null)
        {
            return NotFound();
        }

        return View(trainer);
    }

    // POST: TrainerManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("TrainerId,FullName,Email,Phone,Specialization,HireDate")] Trainer trainer)
    {
        if (id != trainer.TrainerId)
        {
            return NotFound();
        }

        ModelState.Remove("UserId");
        ModelState.Remove("User");

        if (ModelState.IsValid)
        {
            var existingTrainer = await _context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TrainerId == id);

            if (existingTrainer == null)
            {
                return NotFound();
            }

            // If email changed, check for duplicates and synchronize Identity account
            if (!string.Equals(existingTrainer.Email, trainer.Email, StringComparison.OrdinalIgnoreCase))
            {
                var userWithEmail = await _userManager.FindByEmailAsync(trainer.Email);
                if (userWithEmail != null && userWithEmail.Id != existingTrainer.UserId)
                {
                    ModelState.AddModelError("Email", "An account with this email address already exists.");
                    return View(trainer);
                }

                if (await _context.Trainers.AnyAsync(t => t.Email == trainer.Email && t.TrainerId != id))
                {
                    ModelState.AddModelError("Email", "A trainer with this email address already exists.");
                    return View(trainer);
                }

                // Synchronize Identity account Email and UserName
                if (!string.IsNullOrEmpty(existingTrainer.UserId))
                {
                    var user = existingTrainer.User ?? await _userManager.FindByIdAsync(existingTrainer.UserId);
                    if (user != null)
                    {
                        user.Email = trainer.Email;
                        user.UserName = trainer.Email;
                        user.FullName = trainer.FullName;
                        await _userManager.UpdateAsync(user);
                    }
                }
            }
            else
            {
                // Sync FullName on Identity user if FullName changed
                if (!string.IsNullOrEmpty(existingTrainer.UserId))
                {
                    var user = existingTrainer.User ?? await _userManager.FindByIdAsync(existingTrainer.UserId);
                    if (user != null && user.FullName != trainer.FullName)
                    {
                        user.FullName = trainer.FullName;
                        await _userManager.UpdateAsync(user);
                    }
                }
            }

            // Update manageable Trainer fields (preserving UserId)
            existingTrainer.FullName = trainer.FullName;
            existingTrainer.Email = trainer.Email;
            existingTrainer.Phone = trainer.Phone;
            existingTrainer.Specialization = trainer.Specialization;
            existingTrainer.HireDate = trainer.HireDate;

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Trainer '{existingTrainer.FullName}' was updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await TrainerExistsAsync(trainer.TrainerId))
                {
                    return NotFound();
                }
                throw;
            }
        }

        return View(trainer);
    }

    // GET: TrainerManagement/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var trainer = await _context.Trainers
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.WorkoutPlans)
            .FirstOrDefaultAsync(t => t.TrainerId == id);

        if (trainer == null)
        {
            return NotFound();
        }

        return View(trainer);
    }

    // POST: TrainerManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var trainer = await _context.Trainers
            .Include(t => t.WorkoutPlans)
            .FirstOrDefaultAsync(t => t.TrainerId == id);

        if (trainer == null)
        {
            return NotFound();
        }

        // Check for related records due to Restrict foreign key constraints
        if (trainer.WorkoutPlans.Count > 0)
        {
            TempData["ErrorMessage"] = $"Cannot delete trainer '{trainer.FullName}' because they have associated workout plans ({trainer.WorkoutPlans.Count}). Please remove or reassign related workout plans first.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        var linkedUserId = trainer.UserId;

        try
        {
            _context.Trainers.Remove(trainer);
            await _context.SaveChangesAsync();

            // Also delete the linked Identity ApplicationUser account if present
            if (!string.IsNullOrEmpty(linkedUserId))
            {
                var user = await _userManager.FindByIdAsync(linkedUserId);
                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                }
            }

            TempData["SuccessMessage"] = $"Trainer '{trainer.FullName}' and their login account were deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Unable to delete trainer due to database constraints. Please check for related records.";
            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    private async Task<bool> TrainerExistsAsync(int id)
    {
        return await _context.Trainers.AnyAsync(e => e.TrainerId == id);
    }
}
