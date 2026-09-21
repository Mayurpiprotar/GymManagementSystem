namespace GymManagementSystem.Models;

public class Payment
{
    public int PaymentId { get; set; }
    public int MemberId { get; set; }
    public int MembershipId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
