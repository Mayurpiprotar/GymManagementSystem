namespace GymManagementSystem.Controllers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using GymManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class TrainerApplicationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly TrainerDocumentStorage _documentStorage;

    public TrainerApplicationController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        TrainerDocumentStorage documentStorage)
    {
        _context = context;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _documentStorage = documentStorage;
    }

    // =========================================================================
    // PUBLIC APPLICANT WORKFLOW (Anonymous Access Allowed)
    // =========================================================================

    /// <summary>
    /// Displays the public application form for prospective gym trainers.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [Route("TrainerApplication/Create")]
    [Route("ApplyAsTrainer")]
    public async Task<IActionResult> Create()
    {
        var model = new TrainerApplicationCreateViewModel();
        await PopulateAvailableSpecializationsAsync(model);
        return View(model);
    }

    /// <summary>
    /// Handles public submission of trainer applications with full server-side validations,
    /// duplicate email checking, document security scanning, and temporary password hashing.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("TrainerApplication/Create")]
    [Route("ApplyAsTrainer")]
    public async Task<IActionResult> Create(TrainerApplicationCreateViewModel model)
    {
        // 1. Validate Specialization selection (at least one required)
        if (model.SelectedSpecializationIds == null || !model.SelectedSpecializationIds.Any())
        {
            ModelState.AddModelError("SelectedSpecializationIds", "Please select at least one specialization for your application.");
        }

        // 2. Validate that all submitted specialization IDs actually exist in the database
        List<int> validSpecializationIds = new();
        if (model.SelectedSpecializationIds != null && model.SelectedSpecializationIds.Any())
        {
            var dbSpecializationIds = await _context.Specializations
                .AsNoTracking()
                .Select(s => s.SpecializationId)
                .ToListAsync();

            validSpecializationIds = model.SelectedSpecializationIds.Distinct().ToList();
            if (validSpecializationIds.Any(id => !dbSpecializationIds.Contains(id)))
            {
                ModelState.AddModelError("SelectedSpecializationIds", "One or more selected specializations are invalid.");
            }
        }

        // 3. Duplicate checks based on normalized email (case-insensitive)
        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var normalizedEmail = model.Email.Trim().ToLowerInvariant();

            // Check if an Identity user already exists with this email
            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists. Please log in or use a different email address.");
            }

            // Check if a registered Trainer already exists with this email
            var existingTrainer = await _context.Trainers
                .AsNoTracking()
                .AnyAsync(t => t.Email.ToLower() == normalizedEmail);
            if (existingTrainer)
            {
                ModelState.AddModelError("Email", "A registered trainer with this email address already exists.");
            }

            // Check if a Pending application already exists with this email
            var pendingApplication = await _context.TrainerApplications
                .AsNoTracking()
                .AnyAsync(a => a.Email.ToLower() == normalizedEmail && a.Status == GymConstants.TrainerApplicationStatuses.Pending);
            if (pendingApplication)
            {
                ModelState.AddModelError("Email", "A trainer application with this email is currently pending review. Please wait for an administrator to review your application.");
            }
        }

        // 4. Validate Certification Document using TrainerDocumentStorage
        if (model.CertificationDocument == null || model.CertificationDocument.Length == 0)
        {
            ModelState.AddModelError("CertificationDocument", "A certification proof document is required.");
        }
        else
        {
            if (!_documentStorage.ValidateDocument(model.CertificationDocument, out var certError))
            {
                ModelState.AddModelError("CertificationDocument", certError ?? "Invalid certification document.");
            }
        }

        // 5. Validate Experience Document if provided
        if (model.ExperienceDocument != null && model.ExperienceDocument.Length > 0)
        {
            if (!_documentStorage.ValidateDocument(model.ExperienceDocument, out var expError))
            {
                ModelState.AddModelError("ExperienceDocument", expError ?? "Invalid experience document.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAvailableSpecializationsAsync(model);
            return View(model);
        }

        // 6. Secure Document Storage & Database Persistence
        TrainerDocumentSaveResult? certResult = null;
        TrainerDocumentSaveResult? expResult = null;

        try
        {
            // Save certification document to secure App_Data storage
            certResult = await _documentStorage.SaveDocumentAsync(model.CertificationDocument!);

            // Save experience document if provided
            if (model.ExperienceDocument != null && model.ExperienceDocument.Length > 0)
            {
                expResult = await _documentStorage.SaveDocumentAsync(model.ExperienceDocument);
            }

            // Generate secure temporary password hash (plaintext password is NEVER stored)
            var dummyUser = new ApplicationUser { UserName = model.Email, Email = model.Email };
            var passwordHash = _passwordHasher.HashPassword(dummyUser, model.Password);

            var application = new TrainerApplication
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim().ToLowerInvariant(),
                Phone = model.Phone.Trim(),
                ExperienceYears = model.ExperienceYears,
                QualificationsSummary = model.QualificationsSummary.Trim(),
                TemporaryPasswordHash = passwordHash,
                CertificationDocumentPath = certResult.RelativePath,
                CertificationOriginalFileName = certResult.OriginalFileName,
                ExperienceDocumentPath = expResult?.RelativePath,
                ExperienceOriginalFileName = expResult?.OriginalFileName,
                AppliedDate = DateTime.Today,
                Status = GymConstants.TrainerApplicationStatuses.Pending
            };

            foreach (var specId in validSpecializationIds)
            {
                application.ApplicationSpecializations.Add(new TrainerApplicationSpecialization
                {
                    SpecializationId = specId
                });
            }

            _context.TrainerApplications.Add(application);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your trainer application has been submitted successfully. Your application is pending admin review.";
            return RedirectToAction(nameof(Confirmation));
        }
        catch (Exception)
        {
            // Rollback uploaded files on disk if database persistence fails
            if (certResult != null)
            {
                _documentStorage.DeleteDocument(certResult.StorageFileName);
            }
            if (expResult != null)
            {
                _documentStorage.DeleteDocument(expResult.StorageFileName);
            }

            ModelState.AddModelError(string.Empty, "An unexpected error occurred while saving your application. Please try again.");
            await PopulateAvailableSpecializationsAsync(model);
            return View(model);
        }
    }

    /// <summary>
    /// Displays a confirmation screen upon successful application submission.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [Route("TrainerApplication/Confirmation")]
    public IActionResult Confirmation()
    {
        return View();
    }

    // =========================================================================
    // ADMIN WORKFLOW (Admin Role Strictly Required)
    // =========================================================================

    /// <summary>
    /// Lists all trainer applications for admin review.
    /// Supports filtering by Pending, Approved, Rejected, or All.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet]
    [Route("Admin/TrainerApplications")]
    [Route("TrainerApplication/Index")]
    public async Task<IActionResult> Index(string? statusFilter = null)
    {
        var query = _context.TrainerApplications
            .AsNoTracking()
            .Include(a => a.ApplicationSpecializations)
                .ThenInclude(tas => tas.Specialization)
            .Include(a => a.ReviewedByAdmin)
            .Include(a => a.CreatedTrainer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
        {
            query = query.Where(a => a.Status == statusFilter);
        }

        // Show Pending applications first, then ordered by newest AppliedDate
        var applications = await query
            .OrderBy(a => a.Status == GymConstants.TrainerApplicationStatuses.Pending ? 0 : 1)
            .ThenByDescending(a => a.AppliedDate)
            .ThenByDescending(a => a.TrainerApplicationId)
            .ToListAsync();

        ViewData["CurrentFilter"] = statusFilter ?? "All";
        ViewData["PendingCount"] = await _context.TrainerApplications
            .CountAsync(a => a.Status == GymConstants.TrainerApplicationStatuses.Pending);

        return View(applications);
    }

    /// <summary>
    /// Displays comprehensive details of a specific trainer application for admin evaluation.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet]
    [Route("Admin/TrainerApplications/Details/{id}")]
    [Route("TrainerApplication/Details/{id}")]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var application = await _context.TrainerApplications
            .AsNoTracking()
            .Include(a => a.ApplicationSpecializations)
                .ThenInclude(tas => tas.Specialization)
            .Include(a => a.ReviewedByAdmin)
            .Include(a => a.CreatedTrainer)
            .FirstOrDefaultAsync(a => a.TrainerApplicationId == id);

        if (application == null)
        {
            return NotFound();
        }

        return View(application);
    }

    /// <summary>
    /// Securely downloads a trainer application's certification or experience document.
    /// Strictly authorized for Admins only. Never exposes physical server paths.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet]
    [Route("Admin/TrainerApplications/DownloadProof/{id}")]
    [Route("TrainerApplication/DownloadProof/{id}")]
    public async Task<IActionResult> DownloadProof(int id, string type)
    {
        var application = await _context.TrainerApplications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TrainerApplicationId == id);

        if (application == null)
        {
            return NotFound();
        }

        string? relativePath;
        string? originalFileName;

        if (string.Equals(type, "experience", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = application.ExperienceDocumentPath;
            originalFileName = application.ExperienceOriginalFileName ?? "ExperienceDocument";
        }
        else
        {
            relativePath = application.CertificationDocumentPath;
            originalFileName = application.CertificationOriginalFileName;
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return NotFound("No document is associated with this application for the requested type.");
        }

        if (!_documentStorage.FileExists(relativePath))
        {
            return NotFound("The requested document file could not be found on the server.");
        }

        var physicalPath = _documentStorage.GetPhysicalPath(relativePath);
        var extension = Path.GetExtension(physicalPath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

        // Return file securely with original filename header
        return PhysicalFile(physicalPath, contentType, originalFileName);
    }

    /// <summary>
    /// Approves a Pending trainer application.
    /// Transactionally creates the Identity account with precomputed hash, assigns Trainer role,
    /// provisions the Trainer domain profile with specializations, and clears temporary hash.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Admin/TrainerApplications/Approve/{id}")]
    [Route("TrainerApplication/Approve/{id}")]
    public async Task<IActionResult> Approve(int id)
    {
        var application = await _context.TrainerApplications
            .Include(a => a.ApplicationSpecializations)
                .ThenInclude(tas => tas.Specialization)
            .FirstOrDefaultAsync(a => a.TrainerApplicationId == id);

        if (application == null)
        {
            return NotFound();
        }

        if (application.Status != GymConstants.TrainerApplicationStatuses.Pending)
        {
            TempData["ErrorMessage"] = $"Application #{id} cannot be approved because its current status is '{application.Status}'. Only Pending applications can be approved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var normalizedEmail = application.Email.Trim().ToLowerInvariant();

        // Check for conflicting Identity user
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            TempData["ErrorMessage"] = $"Cannot approve application #{id} because an Identity account with email '{application.Email}' already exists.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Check for conflicting Trainer domain record
        var existingTrainer = await _context.Trainers.AnyAsync(t => t.Email.ToLower() == normalizedEmail);
        if (existingTrainer)
        {
            TempData["ErrorMessage"] = $"Cannot approve application #{id} because a Trainer record with email '{application.Email}' already exists.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrEmpty(application.TemporaryPasswordHash))
        {
            TempData["ErrorMessage"] = $"Cannot approve application #{id} because no temporary password hash was preserved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Begin atomic database transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Create Identity User preserving precomputed password hash
            var user = new ApplicationUser
            {
                UserName = application.Email,
                Email = application.Email,
                FullName = application.FullName,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("D"),
                PasswordHash = application.TemporaryPasswordHash
            };

            var userResult = await _userManager.CreateAsync(user);
            if (!userResult.Succeeded)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Failed to create Identity account: {string.Join(", ", userResult.Errors.Select(e => e.Description))}";
                return RedirectToAction(nameof(Details), new { id });
            }

            // 2. Assign Trainer role
            var roleResult = await _userManager.AddToRoleAsync(user, "Trainer");
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Failed to assign Trainer role: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}";
                return RedirectToAction(nameof(Details), new { id });
            }

            // 3. Format specialization summary string for backward compatibility
            var specializationNames = application.ApplicationSpecializations
                .Select(tas => tas.Specialization?.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            var specializationSummary = specializationNames.Any()
                ? string.Join(", ", specializationNames)
                : "General Fitness";

            // 4. Create Trainer domain profile linked to the application
            var trainer = new Trainer
            {
                FullName = application.FullName,
                Email = application.Email,
                Phone = application.Phone,
                Specialization = specializationSummary,
                HireDate = DateTime.Today,
                UserId = user.Id,
                ApplicationId = application.TrainerApplicationId
            };

            _context.Trainers.Add(trainer);
            await _context.SaveChangesAsync();

            // 5. Create TrainerSpecialization join entries
            foreach (var appSpec in application.ApplicationSpecializations)
            {
                _context.TrainerSpecializations.Add(new TrainerSpecialization
                {
                    TrainerId = trainer.TrainerId,
                    SpecializationId = appSpec.SpecializationId
                });
            }

            // 6. Update application status and clear temporary password hash
            var currentAdmin = await _userManager.GetUserAsync(User);
            application.Status = GymConstants.TrainerApplicationStatuses.Approved;
            application.ReviewedDate = DateTime.Today;
            application.ReviewedByAdminId = currentAdmin?.Id;
            application.TemporaryPasswordHash = null; // Cleared permanently upon approval

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Trainer application for '{application.FullName}' has been approved successfully! Trainer account is now active and ready for login.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"An error occurred during approval transaction: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// Rejects a Pending trainer application with an administrative reason.
    /// Does NOT create any Identity user or Trainer domain record.
    /// Clears the temporary password hash and preserves the application for history/audit.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Admin/TrainerApplications/Reject/{id}")]
    [Route("TrainerApplication/Reject/{id}")]
    public async Task<IActionResult> Reject(int id, string? adminNotes)
    {
        var application = await _context.TrainerApplications
            .FirstOrDefaultAsync(a => a.TrainerApplicationId == id);

        if (application == null)
        {
            return NotFound();
        }

        if (application.Status != GymConstants.TrainerApplicationStatuses.Pending)
        {
            TempData["ErrorMessage"] = $"Application #{id} cannot be rejected because its current status is '{application.Status}'. Only Pending applications can be rejected.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var currentAdmin = await _userManager.GetUserAsync(User);

        application.Status = GymConstants.TrainerApplicationStatuses.Rejected;
        application.ReviewedDate = DateTime.Today;
        application.ReviewedByAdminId = currentAdmin?.Id;
        application.AdminNotes = string.IsNullOrWhiteSpace(adminNotes) ? "Application does not meet current criteria." : adminNotes.Trim();
        application.TemporaryPasswordHash = null; // Cleared permanently upon rejection

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Trainer application for '{application.FullName}' has been rejected.";
        return RedirectToAction(nameof(Index));
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private async Task PopulateAvailableSpecializationsAsync(TrainerApplicationCreateViewModel model)
    {
        var specializations = await _context.Specializations
            .AsNoTracking()
            .OrderBy(s => s.SpecializationId)
            .ToListAsync();

        model.AvailableSpecializations = specializations.Select(s => new SpecializationCheckboxItem
        {
            SpecializationId = s.SpecializationId,
            Name = s.Name,
            Description = s.Description,
            IsSelected = model.SelectedSpecializationIds.Contains(s.SpecializationId)
        }).ToList();
    }
}
