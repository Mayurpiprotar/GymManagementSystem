namespace GymManagementSystem.Models;

public class Trainer
{
    public int TrainerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
}
