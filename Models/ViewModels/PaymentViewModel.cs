using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymManagementSystem.Models.ViewModels;

public class PaymentViewModel
{
    public int PaymentId { get; set; }

    [Required(ErrorMessage = "Please select a member.")]
    [Display(Name = "Member")]
    public int MemberId { get; set; }

    [Required(ErrorMessage = "Please select a membership.")]
    [Display(Name = "Membership")]
    public int MembershipId { get; set; }

    [Required(ErrorMessage = "Payment amount is required.")]
    [Range(0.01, 100000, ErrorMessage = "Amount must be between 0.01 and 100,000.00.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Amount ($)")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Payment date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Please select a payment method.")]
    [StringLength(50)]
    [Display(Name = "Payment Method")]
    public string PaymentMethod { get; set; } = "Cash";

    [Required(ErrorMessage = "Please select a payment status.")]
    [StringLength(20)]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Paid";

    // Dropdown collections for UI binding
    public IEnumerable<SelectListItem>? MemberList { get; set; }
    public IEnumerable<SelectListItem>? MembershipList { get; set; }
    public IEnumerable<SelectListItem>? PaymentMethodList { get; set; }
    public IEnumerable<SelectListItem>? StatusList { get; set; }

    // Helper items for client-side filtering by Member
    public List<MembershipSelectItem> AvailableMemberships { get; set; } = new();
}

public class MembershipSelectItem
{
    public int MembershipId { get; set; }
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal PlanPrice { get; set; }
    public string Status { get; set; } = string.Empty;
}
