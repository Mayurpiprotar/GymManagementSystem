using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class Member
{
    public int MemberId { get; set; }

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime DateOfBirth { get; set; }

    [Required]
    [StringLength(20)]
    public string Gender { get; set; } = string.Empty;

    [StringLength(250)]
    public string Address { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime JoinDate { get; set; }

    [StringLength(50)]
    public string? ReferralCode { get; set; }

    [StringLength(50)]
    public string? ReferredByCode { get; set; }

    // Account linkage
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Navigation properties
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<WorkoutPlan> WorkoutPlans { get; set; } = new List<WorkoutPlan>();
}
