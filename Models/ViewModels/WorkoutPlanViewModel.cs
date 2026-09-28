using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymManagementSystem.Models.ViewModels;

public class WorkoutPlanViewModel
{
    public int WorkoutPlanId { get; set; }

    [Required(ErrorMessage = "Please select a member.")]
    [Display(Name = "Member")]
    public int MemberId { get; set; }

    [Required(ErrorMessage = "Plan Name is required.")]
    [StringLength(100, ErrorMessage = "Plan Name cannot exceed 100 characters.")]
    [Display(Name = "Plan Name")]
    public string PlanName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    [Display(Name = "Description / Routine")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Created Date")]
    [DataType(DataType.Date)]
    public DateTime CreatedDate { get; set; } = DateTime.Today;

    [Display(Name = "Trainer")]
    public string? TrainerName { get; set; }

    // Dropdown list of members for server-rendered select
    public IEnumerable<SelectListItem>? MemberList { get; set; }
}
