using GymManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Data;

public class ApplicationDbContext : DbContext
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
    }
}
