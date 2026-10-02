using GymManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<Trainer> Trainers { get; set; } = null!;
    public DbSet<MembershipPlan> MembershipPlans { get; set; } = null!;
    public DbSet<Membership> Memberships { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<WorkoutPlan> WorkoutPlans { get; set; } = null!;
    public DbSet<Specialization> Specializations { get; set; } = null!;
    public DbSet<TrainerSpecialization> TrainerSpecializations { get; set; } = null!;
    public DbSet<TrainerApplication> TrainerApplications { get; set; } = null!;
    public DbSet<TrainerApplicationSpecialization> TrainerApplicationSpecializations { get; set; } = null!;
    public DbSet<Referral> Referrals { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Decimal precision configurations
        modelBuilder.Entity<MembershipPlan>()
            .Property(p => p.Price)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasColumnType("decimal(18,2)");

        // 1. Member -> Membership (One-to-Many)
        modelBuilder.Entity<Membership>()
            .HasOne(m => m.Member)
            .WithMany(m => m.Memberships)
            .HasForeignKey(m => m.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // 2. MembershipPlan -> Membership (One-to-Many)
        modelBuilder.Entity<Membership>()
            .HasOne(m => m.MembershipPlan)
            .WithMany(p => p.Memberships)
            .HasForeignKey(m => m.MembershipPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // 3. Member -> Payment (One-to-Many)
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Member)
            .WithMany(m => m.Payments)
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // 4. Membership -> Payment (One-to-Many)
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Membership)
            .WithMany(m => m.Payments)
            .HasForeignKey(p => p.MembershipId)
            .OnDelete(DeleteBehavior.Restrict);

        // 5. Member -> WorkoutPlan (One-to-Many)
        modelBuilder.Entity<WorkoutPlan>()
            .HasOne(w => w.Member)
            .WithMany(m => m.WorkoutPlans)
            .HasForeignKey(w => w.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // 6. Trainer -> WorkoutPlan (One-to-Many)
        modelBuilder.Entity<WorkoutPlan>()
            .HasOne(w => w.Trainer)
            .WithMany(t => t.WorkoutPlans)
            .HasForeignKey(w => w.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        // 7. Member -> ApplicationUser (One-to-One / Optional)
        modelBuilder.Entity<Member>()
            .HasOne(m => m.User)
            .WithOne()
            .HasForeignKey<Member>(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // 8. Trainer -> ApplicationUser (One-to-One / Optional)
        modelBuilder.Entity<Trainer>()
            .HasOne(t => t.User)
            .WithOne()
            .HasForeignKey<Trainer>(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // 9. TrainerSpecialization (Many-to-Many: Trainer <-> Specialization)
        modelBuilder.Entity<TrainerSpecialization>()
            .HasKey(ts => new { ts.TrainerId, ts.SpecializationId });

        modelBuilder.Entity<TrainerSpecialization>()
            .HasOne(ts => ts.Trainer)
            .WithMany(t => t.TrainerSpecializations)
            .HasForeignKey(ts => ts.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainerSpecialization>()
            .HasOne(ts => ts.Specialization)
            .WithMany(s => s.TrainerSpecializations)
            .HasForeignKey(ts => ts.SpecializationId)
            .OnDelete(DeleteBehavior.Restrict);

        // 10. TrainerApplicationSpecialization (Many-to-Many: TrainerApplication <-> Specialization)
        modelBuilder.Entity<TrainerApplicationSpecialization>()
            .HasKey(tas => new { tas.TrainerApplicationId, tas.SpecializationId });

        modelBuilder.Entity<TrainerApplicationSpecialization>()
            .HasOne(tas => tas.TrainerApplication)
            .WithMany(ta => ta.ApplicationSpecializations)
            .HasForeignKey(tas => tas.TrainerApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainerApplicationSpecialization>()
            .HasOne(tas => tas.Specialization)
            .WithMany(s => s.ApplicationSpecializations)
            .HasForeignKey(tas => tas.SpecializationId)
            .OnDelete(DeleteBehavior.Restrict);

        // 11. Trainer -> TrainerApplication (One-to-One / Optional: 1 -> 0..1)
        modelBuilder.Entity<Trainer>()
            .HasOne(t => t.Application)
            .WithOne(a => a.CreatedTrainer)
            .HasForeignKey<Trainer>(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        // 12. Membership -> Specialization (Training Goal)
        modelBuilder.Entity<Membership>()
            .HasOne(m => m.TrainingGoalSpecialization)
            .WithMany(s => s.Memberships)
            .HasForeignKey(m => m.TrainingGoalSpecializationId)
            .OnDelete(DeleteBehavior.Restrict);

        // 13. Membership -> Trainer (Assigned Trainer)
        modelBuilder.Entity<Membership>()
            .HasOne(m => m.AssignedTrainer)
            .WithMany(t => t.AssignedMemberships)
            .HasForeignKey(m => m.AssignedTrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        // 14. TrainerApplication -> ApplicationUser (Reviewed By Admin)
        modelBuilder.Entity<TrainerApplication>()
            .HasOne(ta => ta.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(ta => ta.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        // 15. Referral configurations
        modelBuilder.Entity<Referral>()
            .HasOne(r => r.ReferredMember)
            .WithMany()
            .HasForeignKey(r => r.ReferredMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Referral>()
            .HasOne(r => r.ReferrerMember)
            .WithMany()
            .HasForeignKey(r => r.ReferrerMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Referral>()
            .HasOne(r => r.ReferrerTrainer)
            .WithMany()
            .HasForeignKey(r => r.ReferrerTrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        // 16. Seed standard Specializations (7 categories)
        modelBuilder.Entity<Specialization>().HasData(
            new Specialization { SpecializationId = 1, Name = "Strength & Hypertrophy", Description = "Strength development and muscle hypertrophy training." },
            new Specialization { SpecializationId = 2, Name = "Weight Loss & Fat Loss", Description = "Training focused on calorie expenditure, conditioning and fat-loss goals." },
            new Specialization { SpecializationId = 3, Name = "General Fitness", Description = "General health, fitness and physical conditioning." },
            new Specialization { SpecializationId = 4, Name = "Muscle Building", Description = "Muscle development and structured resistance training." },
            new Specialization { SpecializationId = 5, Name = "Functional Training", Description = "Movement quality, functional strength and conditioning." },
            new Specialization { SpecializationId = 6, Name = "Flexibility & Mobility", Description = "Mobility, flexibility and movement improvement." },
            new Specialization { SpecializationId = 7, Name = "Sports Conditioning", Description = "Sport-specific conditioning, agility and performance training." }
        );
    }
}
