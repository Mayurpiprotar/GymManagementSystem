using System.Globalization;
using System.Text;
using GymManagementSystem.Models;

namespace GymManagementSystem.Services;

/// <summary>
/// High-performance, self-contained standard PDF generator for payment receipts.
/// Conforms to ISO 32000 / PDF-1.4 specification without external heavy bloatware.
/// </summary>
public class PdfReceiptService
{
    public static byte[] GenerateReceiptPdf(Payment payment)
    {
        var member = payment.Member;
        var membership = payment.Membership;
        var plan = membership?.MembershipPlan;

        var sb = new StringBuilder();
        var streamObjects = new StringBuilder();

        // Canvas setup: coordinates are in points (1 pt = 1/72 inch). Letter: 612 x 792 pt.
        // Helper to append graphics commands to stream
        streamObjects.AppendLine("q"); // Save graphics state

        // Draw top accent banner (Dark Charcoal / Primary Blue)
        streamObjects.AppendLine("0.08 0.12 0.18 rg"); // Fill color: Dark Navy
        streamObjects.AppendLine("36 710 540 50 re f");  // x y w h fill

        // Header Brand Text
        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F2 20 Tf"); // Helvetica-Bold 20pt
        streamObjects.AppendLine("1.0 0.75 0.0 rg"); // Amber / Gold color
        streamObjects.AppendLine("50 728 Td");
        streamObjects.AppendLine("(IRONPULSE FITNESS CLUB) Tj");
        streamObjects.AppendLine("ET");

        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F1 10 Tf"); // Helvetica 10pt
        streamObjects.AppendLine("1.0 1.0 1.0 rg"); // White
        streamObjects.AppendLine("390 728 Td");
        streamObjects.AppendLine("(OFFICIAL PAYMENT RECEIPT) Tj");
        streamObjects.AppendLine("ET");

        // Receipt Metadata Box
        streamObjects.AppendLine("0.95 0.96 0.98 rg"); // Light gray background
        streamObjects.AppendLine("36 620 540 75 re f");
        streamObjects.AppendLine("0.80 0.83 0.88 RG"); // Border color
        streamObjects.AppendLine("1 w");
        streamObjects.AppendLine("36 620 540 75 re S");

        // Metadata Text
        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F2 11 Tf");
        streamObjects.AppendLine("0.1 0.1 0.1 rg");
        streamObjects.AppendLine("50 675 Td");
        streamObjects.AppendLine($"({EscapePdfText("Receipt No: " + payment.TransactionId)}) Tj");
        streamObjects.AppendLine("0 -18 Td");
        streamObjects.AppendLine("/F1 10 Tf");
        streamObjects.AppendLine("0.3 0.3 0.3 rg");
        streamObjects.AppendLine($"({EscapePdfText("Date: " + payment.PaymentDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture))}) Tj");
        streamObjects.AppendLine("0 -16 Td");
        streamObjects.AppendLine($"({EscapePdfText("Payment Method: " + payment.PaymentMethod)}) Tj");
        streamObjects.AppendLine("ET");

        // Status Badge Box (Right side of metadata)
        bool isPaid = payment.Status == "Paid";
        if (isPaid)
        {
            streamObjects.AppendLine("0.13 0.65 0.34 rg"); // Success Green
        }
        else
        {
            streamObjects.AppendLine("0.85 0.20 0.20 rg"); // Danger Red
        }
        streamObjects.AppendLine("440 655 120 28 re f");

        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F2 12 Tf");
        streamObjects.AppendLine("1.0 1.0 1.0 rg"); // White
        streamObjects.AppendLine("465 664 Td");
        streamObjects.AppendLine($"({EscapePdfText(payment.Status.ToUpperInvariant())}) Tj");
        streamObjects.AppendLine("ET");

        // Member Details Section
        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F2 12 Tf");
        streamObjects.AppendLine("0.1 0.1 0.1 rg");
        streamObjects.AppendLine("50 585 Td");
        streamObjects.AppendLine("(BILLED TO:) Tj");
        streamObjects.AppendLine("/F1 10 Tf");
        streamObjects.AppendLine("0.2 0.2 0.2 rg");
        streamObjects.AppendLine("0 -16 Td");
        streamObjects.AppendLine($"({EscapePdfText("Member Name: " + (member?.FullName ?? "N/A"))}) Tj");
        streamObjects.AppendLine("0 -14 Td");
        streamObjects.AppendLine($"({EscapePdfText("Email: " + (member?.Email ?? "N/A"))}) Tj");
        streamObjects.AppendLine("0 -14 Td");
        streamObjects.AppendLine($"({EscapePdfText("Contact: " + (member?.Phone ?? "N/A"))}) Tj");
        streamObjects.AppendLine("ET");

        // Table Header
        streamObjects.AppendLine("0.12 0.18 0.28 rg"); // Header Dark Blue
        streamObjects.AppendLine("36 490 540 25 re f");

        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F2 10 Tf");
        streamObjects.AppendLine("1.0 1.0 1.0 rg");
        streamObjects.AppendLine("50 498 Td");
        streamObjects.AppendLine("(DESCRIPTION / PLAN) Tj");
        streamObjects.AppendLine("260 0 Td");
        streamObjects.AppendLine("(VALIDITY PERIOD) Tj");
        streamObjects.AppendLine("150 0 Td");
        streamObjects.AppendLine("(AMOUNT (INR\\)) Tj");
        streamObjects.AppendLine("ET");

        // Table Row
        streamObjects.AppendLine("0.98 0.98 0.99 rg");
        streamObjects.AppendLine("36 435 540 55 re f");
        streamObjects.AppendLine("0.85 0.87 0.90 RG");
        streamObjects.AppendLine("36 435 540 55 re S");

        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F2 11 Tf");
        streamObjects.AppendLine("0.1 0.1 0.1 rg");
        streamObjects.AppendLine("50 468 Td");
        var planName = plan?.Name ?? "Gym Membership Subscription";
        streamObjects.AppendLine($"({EscapePdfText(planName)}) Tj");
        streamObjects.AppendLine("/F1 9 Tf");
        streamObjects.AppendLine("0.4 0.4 0.4 rg");
        streamObjects.AppendLine("0 -14 Td");
        streamObjects.AppendLine($"({EscapePdfText("Duration: " + (plan?.DurationInMonths ?? 1) + " Month(s)")}) Tj");

        streamObjects.AppendLine("/F1 10 Tf");
        streamObjects.AppendLine("0.2 0.2 0.2 rg");
        streamObjects.AppendLine("260 14 Td");
        var valStart = membership?.StartDate.ToString("yyyy-MM-dd") ?? "N/A";
        var valEnd = membership?.EndDate.ToString("yyyy-MM-dd") ?? "N/A";
        streamObjects.AppendLine($"({EscapePdfText(valStart + " to " + valEnd)}) Tj");

        streamObjects.AppendLine("/F2 12 Tf");
        streamObjects.AppendLine("0.1 0.5 0.2 rg"); // Green text
        streamObjects.AppendLine("150 0 Td");
        streamObjects.AppendLine($"({EscapePdfText("Rs. " + payment.Amount.ToString("0.00", CultureInfo.InvariantCulture))}) Tj");
        streamObjects.AppendLine("ET");

        // Total Summary Box
        streamObjects.AppendLine("0.93 0.95 0.98 rg");
        streamObjects.AppendLine("330 360 246 60 re f");
        streamObjects.AppendLine("0.80 0.84 0.90 RG");
        streamObjects.AppendLine("330 360 246 60 re S");

        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F1 10 Tf");
        streamObjects.AppendLine("0.3 0.3 0.3 rg");
        streamObjects.AppendLine("345 398 Td");
        streamObjects.AppendLine("(Subtotal:) Tj");
        streamObjects.AppendLine("130 0 Td");
        streamObjects.AppendLine($"({EscapePdfText("Rs. " + payment.Amount.ToString("0.00", CultureInfo.InvariantCulture))}) Tj");
        streamObjects.AppendLine("-130 -22 Td");
        streamObjects.AppendLine("/F2 13 Tf");
        streamObjects.AppendLine("0.08 0.12 0.18 rg");
        streamObjects.AppendLine("(Total Paid:) Tj");
        streamObjects.AppendLine("130 0 Td");
        streamObjects.AppendLine("/F2 14 Tf");
        streamObjects.AppendLine("0.13 0.65 0.34 rg");
        streamObjects.AppendLine($"({EscapePdfText("Rs. " + payment.Amount.ToString("0.00", CultureInfo.InvariantCulture))}) Tj");
        streamObjects.AppendLine("ET");

        // Terms & Security Notice
        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F2 9 Tf");
        streamObjects.AppendLine("0.2 0.2 0.2 rg");
        streamObjects.AppendLine("50 290 Td");
        streamObjects.AppendLine("(TERMS & CONDITIONS:) Tj");
        streamObjects.AppendLine("/F1 8 Tf");
        streamObjects.AppendLine("0.4 0.4 0.4 rg");
        streamObjects.AppendLine("0 -13 Td");
        streamObjects.AppendLine("(1. This is a computer-generated digital receipt and requires no physical signature.) Tj");
        streamObjects.AppendLine("0 -11 Td");
        streamObjects.AppendLine("(2. Gym memberships are non-transferable and subject to IronPulse Gym facility guidelines.) Tj");
        streamObjects.AppendLine("0 -11 Td");
        streamObjects.AppendLine("(3. For billing support, contact support@ironpulse.gym or visit the front desk.) Tj");
        streamObjects.AppendLine("ET");

        // Footer Bar
        streamObjects.AppendLine("0.85 0.87 0.90 RG");
        streamObjects.AppendLine("36 80 540 0.5 re S");

        streamObjects.AppendLine("BT");
        streamObjects.AppendLine("/F1 8 Tf");
        streamObjects.AppendLine("0.5 0.5 0.5 rg");
        streamObjects.AppendLine("170 65 Td");
        streamObjects.AppendLine("(IronPulse Fitness Arena | GSTIN: 24AAACI1234D1Z5 | Downtown Athletic Complex) Tj");
        streamObjects.AppendLine("-20 -11 Td");
        streamObjects.AppendLine("(Thank you for choosing IronPulse! Stay committed, stay strong.) Tj");
        streamObjects.AppendLine("ET");

        streamObjects.AppendLine("Q"); // Restore graphics state

        var streamBytes = Encoding.ASCII.GetBytes(streamObjects.ToString());

        // Construct standard PDF document
        var objects = new List<string>();

        // 1 0 obj: Catalog
        objects.Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // 2 0 obj: Pages
        objects.Add("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // 3 0 obj: Page
        objects.Add("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>\nendobj\n");

        // 4 0 obj: Font Helvetica
        objects.Add("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        // 5 0 obj: Font Helvetica-Bold
        objects.Add("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        // 6 0 obj: Stream
        var streamHeader = $"6 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n";
        var streamFooter = "\nendstream\nendobj\n";

        // Build file
        var output = new MemoryStream();
        var writer = new StreamWriter(output, Encoding.ASCII);
        writer.Write("%PDF-1.4\n");
        writer.Flush();

        var xrefOffsets = new List<long> { 0 };

        for (int i = 0; i < objects.Count; i++)
        {
            writer.Flush();
            xrefOffsets.Add(output.Position);
            writer.Write(objects[i]);
            writer.Flush();
        }

        // Write stream object
        writer.Flush();
        xrefOffsets.Add(output.Position);
        writer.Write(streamHeader);
        writer.Flush();
        output.Write(streamBytes, 0, streamBytes.Length);
        writer.Write(streamFooter);
        writer.Flush();

        // Xref table
        long xrefPos = output.Position;
        writer.Write($"xref\n0 {xrefOffsets.Count}\n");
        writer.Write("0000000000 65535 f \n");
        for (int i = 1; i < xrefOffsets.Count; i++)
        {
            writer.Write($"{xrefOffsets[i]:D10} 00000 n \n");
        }

        // Trailer
        writer.Write($"trailer\n<< /Size {xrefOffsets.Count} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");
        writer.Flush();

        return output.ToArray();
    }

    private static string EscapePdfText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            if (c == '(' || c == ')' || c == '\\')
            {
                sb.Append('\\').Append(c);
            }
            else if (c >= 32 && c <= 126)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }
}
