using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TripNest.Core.Models;

namespace TripNest.Core.Pdf;

/// <summary>Renders a payment receipt as a branded PDF.</summary>
public static class ReceiptPdf
{
    public static byte[] Render(Receipt receipt, string? payerName) =>
        Document.Create(container => Compose(container, receipt, payerName)).GeneratePdf();

    private static void Compose(IDocumentContainer container, Receipt r, string? payerName)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3).LineHeight(1.35f));

            page.Header().Element(h => TripNestPdf.Header(h, "Payment Receipt", $"Receipt {r.Id}"));

            page.Content().PaddingVertical(18).Column(col =>
            {
                col.Spacing(16);

                // Amount banner.
                col.Item().Background(TripNestPdf.Brand).Padding(16).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Text("Amount paid").FontColor(TripNestPdf.BrandTint);
                    row.ConstantItem(220).AlignRight().Text($"GHS {r.Amount:N2}")
                        .FontSize(22).Bold().FontColor(Colors.White);
                });

                // Details card.
                col.Item().Border(1).BorderColor(TripNestPdf.Line).Padding(12).Column(c =>
                {
                    c.Item().Element(e => TripNestPdf.Field(e, "Receipt ID", r.Id));
                    c.Item().Element(e => TripNestPdf.Field(e, "Booking ID", r.BookingId));
                    if (!string.IsNullOrWhiteSpace(payerName))
                        c.Item().Element(e => TripNestPdf.Field(e, "Paid by", payerName));
                    if (!string.IsNullOrWhiteSpace(r.PaymentMethod))
                        c.Item().Element(e => TripNestPdf.Field(e, "Payment method", r.PaymentMethod));
                    if (!string.IsNullOrWhiteSpace(r.Description))
                        c.Item().Element(e => TripNestPdf.Field(e, "Description", r.Description));
                    c.Item().Element(e => TripNestPdf.Field(e, "Date", $"{r.CreatedAt:yyyy-MM-dd HH:mm} UTC"));
                });

                col.Item().Text("Thank you for using TripNest.").Italic().FontColor(TripNestPdf.Muted);
            });

            page.Footer().Element(TripNestPdf.Footer);
        });
    }
}
