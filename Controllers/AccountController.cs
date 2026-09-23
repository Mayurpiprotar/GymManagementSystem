using GymManagementSystem.Data;
using GymManagementSystem.Models;
using GymManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GymManagementSystem.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
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
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Public registration only assigns the Member role
            await _userManager.AddToRoleAsync(user, "Member");

            // Create linked Member domain profile
            var member = new Member
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = "000-000-0000",
                DateOfBirth = DateTime.Today.AddYears(-18),
                Gender = "Unspecified",
                Address = "N/A",
                JoinDate = DateTime.Today,
                UserId = user.Id
            };

            _context.Members.Add(member);
            await _context.SaveChangesAsync();

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Member");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
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
}
