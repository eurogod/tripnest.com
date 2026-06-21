using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TripNest.Core.Pdf;

/// <summary>
/// Shared TripNest PDF styling — brand palette plus reusable header/footer/field building blocks
/// so the agreement and receipt documents look consistent.
/// </summary>
internal static class TripNestPdf
{
    public const string Brand = "#0F766E";      // teal
    public const string BrandDark = "#115E59";
    public const string BrandTint = "#D1FAE5";
    public const string Muted = "#6B7280";
    public const string Line = "#E5E7EB";
    public const string Surface = "#F9FAFB";

    /// <summary>Branded header band with the TripNest wordmark and the document title.</summary>
    public static void Header(IContainer container, string title, string subtitle)
    {
        container.Column(col =>
        {
            col.Item().Background(Brand).Padding(16).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("TripNest").FontSize(20).Bold().FontColor(Colors.White);
                    c.Item().Text("Verified rentals in Ghana").FontSize(9).FontColor(BrandTint);
                });
                row.ConstantItem(160).AlignRight().AlignMiddle()
                    .Text(title.ToUpperInvariant()).FontSize(12).Bold().FontColor(Colors.White);
            });
            col.Item().PaddingTop(10).Text(subtitle).FontSize(9).FontColor(Muted);
        });
    }

    /// <summary>Footer with generation timestamp and page x / y.</summary>
    public static void Footer(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Line);
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · TripNest")
                    .FontSize(8).FontColor(Muted);
                row.ConstantItem(90).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });
    }

    /// <summary>A label / value row used inside detail cards.</summary>
    public static void Field(IContainer container, string label, string? value)
    {
        container.PaddingVertical(3).Row(row =>
        {
            row.ConstantItem(140).Text(label).FontColor(Muted);
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
        });
    }
}
