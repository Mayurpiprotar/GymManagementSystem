namespace GymManagementSystem.Services;

using GymManagementSystem.Data;
using GymManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Helper service for calculating trainer workload based strictly on verified active memberships.
/// IMPORTANT: Only verified 'Paid', canonically Active memberships count toward a trainer's client workload.
/// Pending, Failed, Refunded, Upcoming, and Expired memberships are strictly excluded.
/// </summary>
public static class TrainerWorkloadService
{
    /// <summary>
    /// Calculates the number of active clients assigned to a trainer directly from the database.
    /// Only memberships where:
    /// - AssignedTrainerId == trainerId
    /// - Has at least one payment with Status == 'Paid'
    /// - StartDate &lt;= today &lt;= EndDate
    /// are counted.
    /// </summary>
    public static async Task<int> GetActiveTrainerWorkloadAsync(
        ApplicationDbContext dbContext,
        int trainerId,
        DateTime? today = null)
    {
        var currentDate = (today ?? DateTime.Today).Date;

        return await dbContext.Memberships
            .AsNoTracking()
            .Where(m => m.AssignedTrainerId == trainerId
                     && m.StartDate <= currentDate
                     && m.EndDate >= currentDate
                     && m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid))
            .CountAsync();
    }

    /// <summary>
    /// Calculates active client workloads for a collection of trainer IDs in a single batched database query.
    /// Returns a dictionary mapping TrainerId -> ActiveClientCount.
    /// </summary>
    public static async Task<Dictionary<int, int>> GetActiveTrainerWorkloadsAsync(
        ApplicationDbContext dbContext,
        IEnumerable<int> trainerIds,
        DateTime? today = null)
    {
        var currentDate = (today ?? DateTime.Today).Date;
        var idsList = trainerIds.Distinct().ToList();

        var counts = await dbContext.Memberships
            .AsNoTracking()
            .Where(m => m.AssignedTrainerId.HasValue
                     && idsList.Contains(m.AssignedTrainerId.Value)
                     && m.StartDate <= currentDate
                     && m.EndDate >= currentDate
                     && m.Payments.Any(p => p.Status == GymConstants.PaymentStatuses.Paid))
            .GroupBy(m => m.AssignedTrainerId!.Value)
            .Select(g => new { TrainerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TrainerId, x => x.Count);

        var result = new Dictionary<int, int>();
        foreach (var id in idsList)
        {
            result[id] = counts.TryGetValue(id, out var count) ? count : 0;
        }

        return result;
    }

    /// <summary>
    /// Calculates active workload from an in-memory Trainer entity whose AssignedMemberships are loaded.
    /// </summary>
    public static int GetActiveTrainerWorkload(Trainer? trainer, DateTime? today = null)
    {
        if (trainer == null || trainer.AssignedMemberships == null)
        {
            return 0;
        }

        return GetActiveTrainerWorkload(trainer.AssignedMemberships, trainer.TrainerId, today);
    }

    /// <summary>
    /// Calculates active workload from an in-memory collection of memberships for a specified trainer ID.
    /// </summary>
    public static int GetActiveTrainerWorkload(
        IEnumerable<Membership>? memberships,
        int trainerId,
        DateTime? today = null)
    {
        if (memberships == null)
        {
            return 0;
        }

        return memberships.Count(m =>
            m.AssignedTrainerId == trainerId
            && MembershipStatusResolver.IsActiveMembership(m, today));
    }
}
