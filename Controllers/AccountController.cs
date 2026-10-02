using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using GymManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly TrainerDocumentStorage _documentStorage;
    private readonly ReferralService _referralService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        TrainerDocumentStorage documentStorage,
        ReferralService referralService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _documentStorage = documentStorage;
        _referralService = referralService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.EmailOrUsername)
                   ?? await _userManager.FindByNameAsync(model.EmailOrUsername);

        if (user != null)
        {
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                if (roles.Contains("Trainer"))
                {
                    return RedirectToAction("Index", "Trainer");
                }

                return RedirectToAction("Index", "Member");
            }
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your credentials.");
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        var model = new RegisterViewModel();
        await PopulateSpecializationsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // 1. Role-specific validation
        if (model.Role == "Trainer")
        {
            if (model.CertificationDocument == null || model.CertificationDocument.Length == 0)
            {
                ModelState.AddModelError(nameof(model.CertificationDocument), "Please upload a valid certification or experience proof document (PDF, PNG, JPG).");
            }
            else if (!_documentStorage.ValidateDocument(model.CertificationDocument, out var docError))
            {
                ModelState.AddModelError(nameof(model.CertificationDocument), docError ?? "Uploaded document is invalid.");
            }

            if (!model.ExperienceYears.HasValue || model.ExperienceYears < 0)
            {
                ModelState.AddModelError(nameof(model.ExperienceYears), "Please specify your years of fitness training experience.");
            }

            if (string.IsNullOrWhiteSpace(model.QualificationsSummary))
            {
                ModelState.AddModelError(nameof(model.QualificationsSummary), "Please provide a brief summary of your qualifications and certifications.");
            }

            if (model.SelectedSpecializationIds == null || !model.SelectedSpecializationIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedSpecializationIds), "Please select at least one training specialization.");
            }
        }

        // 2. Optional referral code validation
        if (!string.IsNullOrWhiteSpace(model.ReferralCode))
        {
            var referralCheck = await _referralService.ValidateCodeAsync(model.ReferralCode);
            if (!referralCheck.IsValid)
            {
                ModelState.AddModelError(nameof(model.ReferralCode), referralCheck.Message ?? "Invalid referral code.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateSpecializationsAsync(model);
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            await PopulateSpecializationsAsync(model);
            return View(model);
        }

        // 3. Create Identity User
        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            PhoneNumber = model.PhoneNumber.Trim(),
            FullName = model.FullName.Trim()
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await PopulateSpecializationsAsync(model);
            return View(model);
        }

        // 4. Role-based Domain entity provisioning
        if (model.Role == "Trainer")
        {
            await _userManager.AddToRoleAsync(user, "Trainer");

            TrainerDocumentSaveResult? certResult = null;
            if (model.CertificationDocument != null)
            {
                certResult = await _documentStorage.SaveDocumentAsync(model.CertificationDocument);
            }

            // Fetch selected specializations for summary
            var selectedIds = model.SelectedSpecializationIds ?? new List<int>();
            var specs = await _context.Specializations
                .Where(s => selectedIds.Contains(s.SpecializationId))
                .ToListAsync();
            var specSummary = specs.Any() ? string.Join(", ", specs.Select(s => s.Name)) : "General Fitness";

            // Create linked TrainerApplication in Pending status
            var application = new TrainerApplication
            {
                FullName = model.FullName.Trim(),
                Email = normalizedEmail,
                Phone = model.PhoneNumber.Trim(),
                ExperienceYears = model.ExperienceYears ?? 1,
                QualificationsSummary = model.QualificationsSummary?.Trim() ?? string.Empty,
                CertificationDocumentPath = certResult?.RelativePath ?? string.Empty,
                CertificationOriginalFileName = certResult?.OriginalFileName ?? string.Empty,
                AppliedDate = DateTime.Today,
                Status = GymConstants.TrainerApplicationStatuses.Pending
            };

            foreach (var s in specs)
            {
                application.ApplicationSpecializations.Add(new TrainerApplicationSpecialization
                {
                    SpecializationId = s.SpecializationId
                });
            }

            _context.TrainerApplications.Add(application);
            await _context.SaveChangesAsync();

            // Create Trainer domain record in Unverified state
            var trainer = new Trainer
            {
                FullName = model.FullName.Trim(),
                Email = normalizedEmail,
                Phone = model.PhoneNumber.Trim(),
                Specialization = specSummary,
                HireDate = DateTime.Today,
                UserId = user.Id,
                ApplicationId = application.TrainerApplicationId,
                IsVerified = false,
                ReferralCode = $"REF-T{user.Id.Substring(0, Math.Min(6, user.Id.Length)).ToUpper()}"
            };

            _context.Trainers.Add(trainer);
            await _context.SaveChangesAsync();

            foreach (var s in specs)
            {
                _context.TrainerSpecializations.Add(new TrainerSpecialization
                {
                    TrainerId = trainer.TrainerId,
                    SpecializationId = s.SpecializationId
                });
            }

            trainer.ReferralCode = $"REF-T{trainer.TrainerId}";
            await _context.SaveChangesAsync();

            // Sign up -> Login -> User Dashboard (Do NOT auto-sign-in!)
            TempData["SuccessMessage"] = "Trainer registration submitted successfully! Your credentials and experience proof are under review. Please log in to check your verification status.";
            return RedirectToAction(nameof(Login));
        }
        else
        {
            // Default Member registration
            await _userManager.AddToRoleAsync(user, "Member");

            var member = new Member
            {
                FullName = model.FullName.Trim(),
                Email = normalizedEmail,
                Phone = model.PhoneNumber.Trim(),
                DateOfBirth = DateTime.Today.AddYears(-20),
                Gender = "Unspecified",
                Address = "N/A",
                JoinDate = DateTime.Today,
                UserId = user.Id,
                ReferredByCode = string.IsNullOrWhiteSpace(model.ReferralCode) ? null : model.ReferralCode.Trim().ToUpperInvariant()
            };

            _context.Members.Add(member);
            await _context.SaveChangesAsync();

            member.ReferralCode = $"REF-M{member.MemberId}";
            await _context.SaveChangesAsync();

            // Record referral if provided
            if (!string.IsNullOrWhiteSpace(model.ReferralCode))
            {
                await _referralService.RecordReferralOnRegistrationAsync(model.ReferralCode, member.MemberId);
            }

            // Sign up -> Login -> User Dashboard (Do NOT auto-sign-in!)
            TempData["SuccessMessage"] = "Registration successful! Welcome to IronPulse Gym. Please log in to continue to your dashboard.";
            return RedirectToAction(nameof(Login));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task PopulateSpecializationsAsync(RegisterViewModel model)
    {
        var specs = await _context.Specializations
            .AsNoTracking()
            .OrderBy(s => s.SpecializationId)
            .ToListAsync();

        model.AvailableSpecializations = specs.Select(s => new SelectListItem
        {
            Value = s.SpecializationId.ToString(),
            Text = s.Name,
            Selected = model.SelectedSpecializationIds.Contains(s.SpecializationId)
        }).ToList();
    }
}
