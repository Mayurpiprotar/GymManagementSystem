namespace GymManagementSystem.Models;

public class TrainerApplicationSpecialization
{
    public int TrainerApplicationId { get; set; }
    public TrainerApplication? TrainerApplication { get; set; }

    public int SpecializationId { get; set; }
    public Specialization? Specialization { get; set; }
}
