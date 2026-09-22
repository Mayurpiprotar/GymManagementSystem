using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class Trainer
{
    public int TrainerId { get; set; }

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

    [Required]
    [StringLength(100)]
    public string Specialization { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime HireDate { get; set; }

    // Navigation properties
    public ICollection<WorkoutPlan> WorkoutPlans { get; set; } = new List<WorkoutPlan>();
}
