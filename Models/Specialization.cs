using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class Specialization
{
    public int SpecializationId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<TrainerSpecialization> TrainerSpecializations { get; set; } = new List<TrainerSpecialization>();
    public ICollection<TrainerApplicationSpecialization> ApplicationSpecializations { get; set; } = new List<TrainerApplicationSpecialization>();
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
