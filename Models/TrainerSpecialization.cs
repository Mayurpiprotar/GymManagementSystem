namespace GymManagementSystem.Models;

public class TrainerSpecialization
{
    public int TrainerId { get; set; }
    public Trainer? Trainer { get; set; }

    public int SpecializationId { get; set; }
    public Specialization? Specialization { get; set; }
}
