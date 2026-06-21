using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TripNest.Core.Models;

namespace TripNest.Core.Pdf;

/// <summary>Renders a rental agreement as a branded PDF.</summary>
public static class AgreementPdf
{
    public static byte[] Render(Agreement agreement, Booking? booking) =>
        Document.Create(container => Compose(container, agreement, booking)).GeneratePdf();

    private static void Compose(IDocumentContainer container, Agreement a, Booking? booking)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3).LineHeight(1.35f));

            page.Header().Element(h => TripNestPdf.Header(h, "Rental Agreement", $"Agreement reference {a.Id}"));

            page.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(14);

                // Summary card: property, parties, rent and term.
                col.Item().Background(TripNestPdf.Surface).Border(1).BorderColor(TripNestPdf.Line).Padding(12).Column(c =>
                {
                    if (booking?.Property != null)
                    {
                        c.Item().Element(e => TripNestPdf.Field(e, "Property", booking.Property.Title));
                        c.Item().Element(e => TripNestPdf.Field(e, "Location", booking.Property.Location));
                    }
                    if (booking?.Tenant != null)
                        c.Item().Element(e => TripNestPdf.Field(e, "Tenant", booking.Tenant.FullName));
                    c.Item().Element(e => TripNestPdf.Field(e, "Rent", $"GHS {a.RentAmount:N2} / {a.RentFrequency}"));
                    if (a.StartDate.HasValue)
                        c.Item().Element(e => TripNestPdf.Field(e, "Start date", a.StartDate.Value.ToString("yyyy-MM-dd")));
                    if (a.EndDate.HasValue)
                        c.Item().Element(e => TripNestPdf.Field(e, "End date", a.EndDate.Value.ToString("yyyy-MM-dd")));
                    c.Item().Element(e => TripNestPdf.Field(e, "Status", a.Status.ToString()));
                    c.Item().Element(e => TripNestPdf.Field(e, "Created", $"{a.CreatedAt:yyyy-MM-dd HH:mm} UTC"));
                });

                // Terms.
                col.Item().Text("Terms & Conditions").FontSize(12).Bold().FontColor(TripNestPdf.BrandDark);
                col.Item().Text(string.IsNullOrWhiteSpace(a.TermsContent) ? "No terms recorded." : a.TermsContent);

                // Signatures.
                col.Item().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem().Element(e => Signature(e, "Tenant", a.TenantSignature, a.SignedAt));
                    row.ConstantItem(28);
                    row.RelativeItem().Element(e => Signature(e, "Landlord", a.LandlordSignature, a.SignedAt));
                });
            });

            page.Footer().Element(TripNestPdf.Footer);
        });
    }

    private static void Signature(IContainer container, string role, string? signature, DateTime? signedAt)
    {
        container.Column(col =>
        {
            col.Item().Height(22).AlignBottom().Text(signature ?? string.Empty).FontSize(13).Italic();
            col.Item().LineHorizontal(0.8f).LineColor(TripNestPdf.Muted);
            col.Item().PaddingTop(2).Text(role).Bold();
            col.Item().Text(signature == null
                ? "Not signed"
                : signedAt.HasValue ? $"Signed {signedAt:yyyy-MM-dd}" : "Signed")
                .FontSize(8).FontColor(TripNestPdf.Muted);
        });
    }
}
