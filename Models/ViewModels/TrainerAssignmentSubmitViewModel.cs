using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// Submission model when Admin assigns or reassigns a trainer to an eligible membership.
/// </summary>
public class TrainerAssignmentSubmitViewModel
{
    [Required]
    public int MembershipId { get; set; }

    [Required]
    public int TrainerId { get; set; }

    /// <summary>
    /// Explicit confirmation flag required when assigning a trainer who does not have an exact specialization match.
    /// </summary>
    public bool ConfirmNonExact { get; set; }
}
