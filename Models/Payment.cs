using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class Payment
{
    public int PaymentId { get; set; }

    public int MemberId { get; set; }

    public int MembershipId { get; set; }

    [Range(0.01, 100000)]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    public DateTime PaymentDate { get; set; }

    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    [StringLength(100)]
    public string? TransactionId { get; set; }

    [StringLength(250)]
    public string? Notes { get; set; }

    // Navigation properties
    public Member? Member { get; set; }
    public Membership? Membership { get; set; }
}
