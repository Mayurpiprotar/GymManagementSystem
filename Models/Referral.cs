using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymManagementSystem.Models;

public class Referral
{
    public int ReferralId { get; set; }

    [Required]
    [StringLength(50)]
    public string ReferrerCode { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string ReferrerRole { get; set; } = "Member"; // "Member" or "Trainer"

    public int? ReferrerMemberId { get; set; }
    public Member? ReferrerMember { get; set; }

    public int? ReferrerTrainerId { get; set; }
    public Trainer? ReferrerTrainer { get; set; }

    public int ReferredMemberId { get; set; }
    public Member? ReferredMember { get; set; }

    [DataType(DataType.Date)]
    public DateTime ReferralDate { get; set; } = DateTime.Today;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountPercent { get; set; } = 10.0m;

    public bool RewardClaimed { get; set; } = false;

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Registered"; // "Registered", "Purchased", "Rewarded"
}
