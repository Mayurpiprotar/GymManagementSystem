using GymManagementSystem.Data;
using GymManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace GymManagementSystem.Services;

public class ReferralValidationResult
{
    public bool IsValid { get; set; }
    public string? ReferrerRole { get; set; } // "Member" or "Trainer"
    public string? ReferrerName { get; set; }
    public int? ReferrerMemberId { get; set; }
    public int? ReferrerTrainerId { get; set; }
    public decimal DiscountPercent { get; set; } = 10.0m;
    public string? Message { get; set; }
}

public class MemberReferralSummary
{
    public string ReferralCode { get; set; } = string.Empty;
    public int TotalReferredFriends { get; set; }
    public decimal NextRenewalDiscountPercent { get; set; }
    public bool HasWelcomeDiscountAvailable { get; set; }
}

public class TrainerMonthlyBonusSummary
{
    public string ReferralCode { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public int MonthlyReferralCount { get; set; }
    public bool IsBonusActive { get; set; }
    public decimal BonusPerMember { get; set; }
    public decimal TotalBonusEarned { get; set; }
    public string PromotionTier { get; set; } = "Standard Coach";
    public int MembersNeededToActivate { get; set; }

    public int CurrentMonthReferrals => MonthlyReferralCount;
    public decimal BonusAmount => TotalBonusEarned;
    public bool HasReachedMinimum => IsBonusActive;
    public bool HasPromotionQualified => MonthlyReferralCount >= 10;
    public string MonthYear => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
}

public class ReferralService
{
    private readonly ApplicationDbContext _context;

    public ReferralService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Validates a referral code against active members and trainers.
    /// </summary>
    public async Task<ReferralValidationResult> ValidateCodeAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new ReferralValidationResult { IsValid = false, Message = "No code provided." };
        }

        var normalizedCode = code.Trim().ToUpperInvariant();

        // 1. Check Trainers
        var trainer = await _context.Trainers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ReferralCode != null && t.ReferralCode.ToUpper() == normalizedCode);

        if (trainer != null)
        {
            return new ReferralValidationResult
            {
                IsValid = true,
                ReferrerRole = "Trainer",
                ReferrerName = trainer.FullName,
                ReferrerTrainerId = trainer.TrainerId,
                DiscountPercent = 10.0m,
                Message = $"Valid referral code from Coach {trainer.FullName}. 10% welcome discount applied!"
            };
        }

        // 2. Check Members
        var member = await _context.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ReferralCode != null && m.ReferralCode.ToUpper() == normalizedCode);

        if (member != null)
        {
            return new ReferralValidationResult
            {
                IsValid = true,
                ReferrerRole = "Member",
                ReferrerName = member.FullName,
                ReferrerMemberId = member.MemberId,
                DiscountPercent = 10.0m,
                Message = $"Valid referral code from member {member.FullName}. 10% welcome discount applied!"
            };
        }

        return new ReferralValidationResult
        {
            IsValid = false,
            Message = "Invalid referral code. Please check the code and try again."
        };
    }

    /// <summary>
    /// Records a new referral when a member registers with a referral code.
    /// </summary>
    public async Task RecordReferralOnRegistrationAsync(string referralCode, int newMemberId)
    {
        var validation = await ValidateCodeAsync(referralCode);
        if (!validation.IsValid) return;

        var referral = new Referral
        {
            ReferrerCode = referralCode.Trim().ToUpperInvariant(),
            ReferrerRole = validation.ReferrerRole ?? "Member",
            ReferrerMemberId = validation.ReferrerMemberId,
            ReferrerTrainerId = validation.ReferrerTrainerId,
            ReferredMemberId = newMemberId,
            ReferralDate = DateTime.Today,
            DiscountPercent = validation.DiscountPercent,
            Status = "Registered",
            RewardClaimed = false
        };

        _context.Referrals.Add(referral);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Calculates discount applicable for a member (welcome discount on first purchase, or reward discount on renewal).
    /// </summary>
    public async Task<decimal> CalculateDiscountPercentAsync(int memberId, bool isRenewal)
    {
        var member = await _context.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == memberId);

        if (member == null) return 0m;

        if (isRenewal)
        {
            // Renewal discount based on friends referred by this member
            var friendsReferredCount = await _context.Referrals
                .CountAsync(r => r.ReferrerMemberId == memberId);

            if (friendsReferredCount >= 4) return 20.0m;
            if (friendsReferredCount >= 2) return 15.0m;
            if (friendsReferredCount >= 1) return 10.0m;
            return 0m;
        }
        else
        {
            // New purchase welcome discount if referred
            if (!string.IsNullOrWhiteSpace(member.ReferredByCode))
            {
                var unclaimedWelcome = await _context.Referrals
                    .AnyAsync(r => r.ReferredMemberId == memberId && !r.RewardClaimed);
                if (unclaimedWelcome)
                {
                    return 10.0m; // 10% welcome discount
                }
            }
            return 0m;
        }
    }

    /// <summary>
    /// Retrieves full member referral summary for dashboard display.
    /// </summary>
    public async Task<MemberReferralSummary> GetMemberSummaryAsync(int memberId)
    {
        var member = await _context.Members.FindAsync(memberId);
        var code = member?.ReferralCode ?? $"REF-M{memberId}";

        var totalFriends = await _context.Referrals
            .CountAsync(r => r.ReferrerMemberId == memberId);

        decimal renewalDiscount = 0m;
        if (totalFriends >= 4) renewalDiscount = 20.0m;
        else if (totalFriends >= 2) renewalDiscount = 15.0m;
        else if (totalFriends >= 1) renewalDiscount = 10.0m;

        bool welcomeAvailable = !string.IsNullOrWhiteSpace(member?.ReferredByCode) &&
            await _context.Referrals.AnyAsync(r => r.ReferredMemberId == memberId && !r.RewardClaimed);

        return new MemberReferralSummary
        {
            ReferralCode = code,
            TotalReferredFriends = totalFriends,
            NextRenewalDiscountPercent = renewalDiscount,
            HasWelcomeDiscountAvailable = welcomeAvailable
        };
    }

    /// <summary>
    /// Computes monthly trainer performance bonus and promotion tier based on referrals.
    /// Business Rules:
    /// - Minimum 2 referrals in the month required to activate bonus.
    /// - 2-4: 500/member bonus (Bronze Coach Promoter).
    /// - 5-9: 750/member bonus (Silver Coach Promoter).
    /// - 10+: 1000/member bonus + Elite Master Trainer promotion.
    /// </summary>
    public async Task<TrainerMonthlyBonusSummary> GetTrainerMonthlyBonusAsync(int trainerId, int? year = null, int? month = null)
    {
        var targetYear = year ?? DateTime.Today.Year;
        var targetMonth = month ?? DateTime.Today.Month;

        var trainer = await _context.Trainers.FindAsync(trainerId);
        var code = trainer?.ReferralCode ?? $"REF-T{trainerId}";

        var startOfMonth = new DateTime(targetYear, targetMonth, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        var monthlyCount = await _context.Referrals
            .CountAsync(r => r.ReferrerTrainerId == trainerId
                          && r.ReferralDate >= startOfMonth
                          && r.ReferralDate <= endOfMonth);

        bool isActive = monthlyCount >= 2;
        decimal bonusPerMember = 0m;
        string tier = "Standard Coach";

        if (monthlyCount >= 10)
        {
            bonusPerMember = 1000.0m;
            tier = "🌟 Elite Master Trainer (Promoted)";
        }
        else if (monthlyCount >= 5)
        {
            bonusPerMember = 750.0m;
            tier = "🥈 Silver Coach Promoter";
        }
        else if (monthlyCount >= 2)
        {
            bonusPerMember = 500.0m;
            tier = "🥉 Bronze Coach Promoter";
        }

        decimal totalBonus = isActive ? (monthlyCount * bonusPerMember) : 0m;
        int needed = monthlyCount < 2 ? (2 - monthlyCount) : 0;

        return new TrainerMonthlyBonusSummary
        {
            ReferralCode = code,
            Month = targetMonth,
            Year = targetYear,
            MonthlyReferralCount = monthlyCount,
            IsBonusActive = isActive,
            BonusPerMember = bonusPerMember,
            TotalBonusEarned = totalBonus,
            PromotionTier = tier,
            MembersNeededToActivate = needed
        };
    }
}
