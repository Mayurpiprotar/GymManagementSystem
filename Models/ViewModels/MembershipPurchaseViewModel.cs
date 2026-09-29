using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// Dedicated ViewModel for submitting training preferences and purchasing a membership plan.
/// Server-controlled fields such as Price, Dates, MemberId, and Status are strictly excluded from client binding.
/// </summary>
public class MembershipPurchaseViewModel
{
    [Required(ErrorMessage = "Please select a membership plan.")]
    public int MembershipPlanId { get; set; }

    [Required(ErrorMessage = "Please select your primary training goal.")]
    [Display(Name = "Training Goal / Specialization")]
    public int TrainingGoalSpecializationId { get; set; }

    [Required(ErrorMessage = "Please select your fitness experience level.")]
    [Display(Name = "Experience Level")]
    public string ExperienceLevel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your training preference.")]
    [StringLength(250, ErrorMessage = "Training preference cannot exceed 250 characters.")]
    [Display(Name = "Training Preference / Notes")]
    public string TrainingPreference { get; set; } = string.Empty;

    // Server-populated display metadata (read-only for UI presentation)
    public string? PlanName { get; set; }
    public int PlanDurationInMonths { get; set; }
    public decimal PlanPrice { get; set; }
    public string? PlanDescription { get; set; }

    public IEnumerable<SelectListItem> AvailableGoals { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> AvailableExperienceLevels { get; set; } = new List<SelectListItem>();
}
