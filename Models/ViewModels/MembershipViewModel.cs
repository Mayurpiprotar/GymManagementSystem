using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymManagementSystem.Models.ViewModels;

public class MembershipViewModel
{
    public int MembershipId { get; set; }

    [Required(ErrorMessage = "Please select a member.")]
    [Display(Name = "Member")]
    public int MemberId { get; set; }

    [Required(ErrorMessage = "Please select a membership plan.")]
    [Display(Name = "Membership Plan")]
    public int MembershipPlanId { get; set; }

    [Required(ErrorMessage = "Start Date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime? EndDate { get; set; }

    [Display(Name = "Status")]
    public string? Status { get; set; }

    // Dropdown collections for UI binding
    public IEnumerable<SelectListItem>? MemberList { get; set; }
    public IEnumerable<SelectListItem>? PlanList { get; set; }
}
