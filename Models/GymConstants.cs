namespace GymManagementSystem.Models;

/// <summary>
/// Centralized constants for the GymManagementSystem domain to eliminate magic strings across controllers, models, and services.
/// </summary>
public static class GymConstants
{
    /// <summary>
    /// Conceptual and database statuses for a Membership subscription.
    /// Canonical status is dynamically evaluated by MembershipStatusResolver based on verified payments and date boundaries.
    /// </summary>
    public static class MembershipStatuses
    {
        public const string PendingPayment = "PendingPayment";
        public const string Upcoming = "Upcoming";
        public const string Active = "Active";
        public const string Expired = "Expired";

        public static readonly string[] All = [PendingPayment, Upcoming, Active, Expired];
    }

    /// <summary>
    /// Status values for Payment records.
    /// Only 'Paid' represents a verified, successful financial transaction.
    /// </summary>
    public static class PaymentStatuses
    {
        public const string Pending = "Pending";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
        public const string Refunded = "Refunded";

        public static readonly string[] All = [Pending, Paid, Failed, Refunded];
    }

    /// <summary>
    /// Accepted payment methods for online purchases and administrative logging.
    /// </summary>
    public static class PaymentMethods
    {
        public const string DemoGateway = "Demo Gateway";
        public const string Card = "Card";
        public const string UPI = "UPI";
        public const string BankTransfer = "Bank Transfer";
        public const string Cash = "Cash";

        public static readonly string[] All = [DemoGateway, Card, UPI, BankTransfer, Cash];
    }

    /// <summary>
    /// Workflow lifecycle statuses for a Trainer application.
    /// </summary>
    public static class TrainerApplicationStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";

        public static readonly string[] All = [Pending, Approved, Rejected];
    }

    /// <summary>
    /// Experience level options for member training preferences.
    /// </summary>
    public static class ExperienceLevels
    {
        public const string Beginner = "Beginner";
        public const string Intermediate = "Intermediate";
        public const string Advanced = "Advanced";

        public static readonly string[] All = [Beginner, Intermediate, Advanced];
    }

    /// <summary>
    /// Document storage settings and constraints for trainer certification uploads.
    /// Documents are strictly stored outside wwwroot (under App_Data/TrainerDocuments).
    /// </summary>
    public static class TrainerDocuments
    {
        public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        public const string StorageDirectoryRelativePath = "App_Data/TrainerDocuments";
        public static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    }
}

