using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TripNest.Core.Models;

namespace TripNest.Core.Pdf;

/// <summary>
/// Renders a verified user's TripNest ID card — an ISO ID-1 (credit-card-ratio) PDF showing their
/// photo (or initials), name, role, TripNestId and a QR code that encodes the TripNestId for
/// scan-to-verify. Only meaningful for users who already hold a TripNestId.
/// </summary>
public static class IdCardPdf
{
    public static byte[] Render(User user, byte[]? photoBytes) =>
        Document.Create(c => Compose(c, user, photoBytes)).GeneratePdf();

    private static void Compose(IDocumentContainer container, User user, byte[]? photoBytes)
    {
        var qr = BuildQr(user.TripNestId ?? user.Id);

        container.Page(page =>
        {
            // ID-1 ratio (85.6 × 54mm) scaled up to ~340 × 214 pt.
            page.Size(340, 214, Unit.Point);
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

            page.Content().Column(col =>
            {
                // Branded header band.
                col.Item().Background(TripNestPdf.Brand).PaddingVertical(8).PaddingHorizontal(14).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Text("TRIPNEST").FontSize(15).Bold().FontColor(Colors.White);
                    row.ConstantItem(140).AlignRight().AlignMiddle()
                        .Text("VERIFIED MEMBER ID").FontSize(8).Bold().FontColor(TripNestPdf.BrandTint);
                });

                // Body: photo · details · QR.
                col.Item().PaddingHorizontal(14).PaddingTop(12).Row(row =>
                {
                    row.Spacing(12);

                    row.ConstantItem(70).Height(70).Element(e => Photo(e, photoBytes, user.FullName));

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(user.FullName).FontSize(13).Bold();
                        c.Item().Text(user.Role.ToString()).FontColor(TripNestPdf.Muted);
                        c.Item().PaddingTop(8).Text("TRIPNEST ID").FontSize(7).Bold().FontColor(TripNestPdf.Muted);
                        c.Item().Text(user.TripNestId ?? "—").FontSize(13).Bold().FontColor(TripNestPdf.BrandDark);
                        c.Item().PaddingTop(6).Text($"Member since {user.CreatedAt:MMM yyyy}")
                            .FontSize(8).FontColor(TripNestPdf.Muted);
                    });

                    row.ConstantItem(58).Column(c =>
                    {
                        c.Item().Width(56).Height(56).Image(qr);
                        c.Item().AlignCenter().Text("Scan to verify").FontSize(6).FontColor(TripNestPdf.Muted);
                    });
                });

                col.Item().PaddingTop(8).PaddingHorizontal(14).Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(TripNestPdf.Line);
                    c.Item().PaddingTop(4)
                        .Text("Identity verified via TripNest.Id (Ghana Card). Non-transferable.")
                        .FontSize(6.5f).FontColor(TripNestPdf.Muted);
                });
            });
        });
    }

    private static void Photo(IContainer container, byte[]? photoBytes, string fullName)
    {
        if (photoBytes is { Length: > 0 })
        {
            container.Border(1).BorderColor(TripNestPdf.Line).Image(photoBytes).FitArea();
        }
        else
        {
            container.Background(TripNestPdf.Brand).AlignCenter().AlignMiddle()
                .Text(Initials(fullName)).FontSize(24).Bold().FontColor(Colors.White);
        }
    }

    private static string Initials(string fullName)
    {
        var parts = (fullName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "?";
        var first = parts[0][..1];
        var last = parts.Length > 1 ? parts[^1][..1] : string.Empty;
        return (first + last).ToUpperInvariant();
    }

    private static byte[] BuildQr(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(20);
    }
}
