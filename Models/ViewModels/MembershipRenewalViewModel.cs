using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// View model for the Member renewal workflow.
/// Displays context from the original membership while accepting renewed preferences and plan selection.
/// Crucial business values (MemberId, StartDate, EndDate, Price, Trainer Preservation) are strictly authoritative server-side.
/// </summary>
public class MembershipRenewalViewModel
{
    // Context from original membership (Display Only)
    public int OriginalMembershipId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;

    public string OriginalPlanName { get; set; } = string.Empty;
    public DateTime OriginalStartDate { get; set; }
    public DateTime OriginalEndDate { get; set; }
    public string OriginalCanonicalStatus { get; set; } = string.Empty;

    public int? PreservedTrainerId { get; set; }
    public string? PreservedTrainerName { get; set; }
    public string? PreservedTrainerEmail { get; set; }
    public bool HasAssignedTrainer => PreservedTrainerId.HasValue && !string.IsNullOrEmpty(PreservedTrainerName);

    // Renewable inputs
    [Required(ErrorMessage = "Please select a membership plan for renewal.")]
    [Display(Name = "Membership Plan")]
    public int SelectedPlanId { get; set; }
    public IEnumerable<SelectListItem> AvailablePlans { get; set; } = new List<SelectListItem>();

    [Required(ErrorMessage = "Please select your primary training goal.")]
    [Display(Name = "Training Goal")]
    public int TrainingGoalSpecializationId { get; set; }
    public IEnumerable<SelectListItem> AvailableGoals { get; set; } = new List<SelectListItem>();

    [Required(ErrorMessage = "Please select your experience level.")]
    [StringLength(50)]
    [Display(Name = "Experience Level")]
    public string ExperienceLevel { get; set; } = string.Empty;
    public IEnumerable<SelectListItem> AvailableExperienceLevels { get; set; } = new List<SelectListItem>();

    [StringLength(250, ErrorMessage = "Training preferences cannot exceed 250 characters.")]
    [Display(Name = "Special Training Preferences / Requests")]
    public string? TrainingPreference { get; set; }

    // Server-calculated preview properties
    public DateTime CalculatedStartDate { get; set; }
    public DateTime CalculatedEndDate { get; set; }
    public decimal PlanPrice { get; set; }
    public int PlanDurationInMonths { get; set; }
}
