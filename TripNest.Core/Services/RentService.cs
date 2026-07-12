using TripNest.Core.DTOs.Rent;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using Microsoft.Extensions.Options;
using TripNest.Core.Options;

namespace TripNest.Core.Services;

/// <summary>
/// Monthly rent collection for long-term stays. The booking's escrow covers only the first
/// 30-day period; this service owns everything after it: the invoice schedule, per-month tenant
/// checkouts (provider metadata "rent:{invoiceId}" routes their webhooks here), due/overdue
/// nudges, and the landlord's per-month payout (net of the platform fee) the moment rent lands.
/// </summary>
public class RentService : IRentService
{
    private readonly IRepository<RentInvoice> _invoiceRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPayoutService _payoutService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly PlatformOptions _platform;
    private readonly ILogger<RentService> _logger;

    public RentService(
        IRepository<RentInvoice> invoiceRepository,
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IPaymentGateway paymentGateway,
        IPayoutService payoutService,
        INotificationService notificationService,
        IConfiguration configuration,
        IOptions<PlatformOptions> platformOptions,
        ILogger<RentService> logger)
    {
        _invoiceRepository = invoiceRepository;
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _paymentGateway = paymentGateway;
        _payoutService = payoutService;
        _notificationService = notificationService;
        _configuration = configuration;
        _platform = platformOptions.Value;
        _logger = logger;
    }

    private int ReminderDays => _configuration.GetValue("Rent:DueReminderDays", 3);

    public async Task<List<RentInvoice>> BuildScheduleAsync(Booking booking, string landlordId, decimal monthlyRent)
    {
        var invoices = new List<RentInvoice>();
        var periodStart = booking.CheckInDate.Date.AddDays(IRentService.RentPeriodDays); // period 1 is the escrow
        while (periodStart < booking.CheckOutDate.Date)
        {
            var periodEnd = periodStart.AddDays(IRentService.RentPeriodDays);
            if (periodEnd > booking.CheckOutDate.Date)
                periodEnd = booking.CheckOutDate.Date;

            var days = (periodEnd - periodStart).Days;
            var amount = days == IRentService.RentPeriodDays
                ? monthlyRent
                : Math.Round(monthlyRent / IRentService.RentPeriodDays * days, 2); // pro-rated final partial month

            invoices.Add(new RentInvoice
            {
                BookingId = booking.Id,
                TenantId = booking.TenantId,
                LandlordId = landlordId,
                PeriodStart = DateTime.SpecifyKind(periodStart, DateTimeKind.Utc),
                PeriodEnd = DateTime.SpecifyKind(periodEnd, DateTimeKind.Utc),
                Amount = amount,
                DueDate = DateTime.SpecifyKind(periodStart, DateTimeKind.Utc)
            });
            periodStart = periodStart.AddDays(IRentService.RentPeriodDays);
        }

        foreach (var invoice in invoices)
            await _invoiceRepository.AddAsync(invoice);
        return invoices;
    }

    public async Task<PagedResult<RentInvoiceResponse>> GetMyInvoicesAsync(string tenantId, int page, int pageSize)
    {
        var invoices = (await _invoiceRepository.FindAsync(i => i.TenantId == tenantId))
            .OrderBy(i => i.DueDate)
            .ToList();
        var paged = Paging.Page(invoices, page, pageSize);
        var propertyIds = await PropertyIdsForAsync(paged.Items);
        return new PagedResult<RentInvoiceResponse>
        {
            Items = paged.Items.Select(i => Map(i, propertyIds.GetValueOrDefault(i.BookingId, string.Empty))).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<List<RentInvoiceResponse>> GetForBookingAsync(string bookingId, string userId)
    {
        var invoices = (await _invoiceRepository.FindAsync(i => i.BookingId == bookingId))
            .OrderBy(i => i.PeriodStart)
            .ToList();
        if (invoices.Count == 0)
            throw new NotFoundException("Rent schedule");

        if (invoices[0].TenantId != userId && invoices[0].LandlordId != userId)
            throw new ForbiddenException("You are not part of this booking");

        var propertyIds = await PropertyIdsForAsync(invoices);
        return invoices.Select(i => Map(i, propertyIds.GetValueOrDefault(i.BookingId, string.Empty))).ToList();
    }

    public async Task<RentInvoiceResponse> InitiatePaymentAsync(string invoiceId, string userId)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId)
            ?? throw new NotFoundException("Rent invoice");
        if (invoice.TenantId != userId)
            throw new ForbiddenException("This invoice is not yours");
        if (invoice.Status == RentInvoiceStatus.Paid)
            return Map(invoice, (await PropertyIdsForAsync(new[] { invoice })).GetValueOrDefault(invoice.BookingId, string.Empty));
        if (invoice.Status == RentInvoiceStatus.Cancelled)
            throw new ValidationException("This invoice was voided when the booking was cancelled");

        var tenant = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");
        var payment = await _paymentGateway.InitiatePaymentAsync(
            invoice.Amount, _platform.Currency, tenant.Email, $"{IRentService.ReferencePrefix}{invoice.Id}");
        if (!payment.Success)
            throw new ValidationException("The payment provider could not start the checkout. Please retry.");

        invoice.PaymentReference = payment.Reference;
        await _invoiceRepository.UpdateAsync(invoice);
        await _invoiceRepository.SaveChangesAsync();

        var response = Map(invoice, (await PropertyIdsForAsync(new[] { invoice })).GetValueOrDefault(invoice.BookingId, string.Empty));
        response.CheckoutUrl = payment.CheckoutUrl;
        return response;
    }

    public async Task<RentInvoiceResponse> VerifyPaymentAsync(string invoiceId, string userId)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId)
            ?? throw new NotFoundException("Rent invoice");
        if (invoice.TenantId != userId)
            throw new ForbiddenException("This invoice is not yours");
        if (invoice.Status == RentInvoiceStatus.Paid)
            return Map(invoice, (await PropertyIdsForAsync(new[] { invoice })).GetValueOrDefault(invoice.BookingId, string.Empty));
        if (string.IsNullOrEmpty(invoice.PaymentReference))
            throw new ValidationException("No payment has been started for this invoice yet");

        var result = await _paymentGateway.VerifyPaymentAsync(invoice.PaymentReference);
        if (!result.Success)
            throw new ValidationException("Payment has not been completed for this invoice yet");

        // Simulated verifies (dev-only gateway) can't know the amount — substitute the expected one.
        await ApplyRentPaymentAsync(invoice.Id, invoice.PaymentReference, result.Simulated ? invoice.Amount : result.Amount);
        var refreshed = (await _invoiceRepository.GetByIdAsync(invoice.Id))!;
        return Map(refreshed, (await PropertyIdsForAsync(new[] { refreshed })).GetValueOrDefault(refreshed.BookingId, string.Empty));
    }

    public async Task ApplyRentPaymentAsync(string invoiceId, string reference, decimal paidAmount)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId)
            ?? throw new InvalidOperationException($"Rent invoice '{invoiceId}' not found");

        // Providers retry webhooks — an already-paid invoice is a successful no-op.
        if (invoice.Status == RentInvoiceStatus.Paid)
            return;

        // Rent charged against a voided invoice is real money with no tenancy — refund it.
        if (invoice.Status == RentInvoiceStatus.Cancelled)
        {
            _logger.LogWarning("Rent paid (ref {Reference}) for cancelled invoice {InvoiceId} — refunding", reference, invoiceId);
            if (!await _paymentGateway.RefundAsync(reference, paidAmount))
                throw new InvalidOperationException($"Late rent payment '{reference}' could not be refunded — manual reconciliation required");
            return;
        }

        // Same 1-pesewa tolerance as every other money path.
        if (Math.Abs(paidAmount - invoice.Amount) > 0.01m)
            throw new InvalidOperationException(
                $"Paid amount ({paidAmount:0.00}) does not match this invoice ({invoice.Amount:0.00})");

        invoice.Status = RentInvoiceStatus.Paid;
        invoice.PaymentReference = reference;
        invoice.PaidAt = DateTime.UtcNow;
        await _invoiceRepository.UpdateAsync(invoice);
        await _invoiceRepository.SaveChangesAsync();

        // Rent disburses immediately (the tenant already lives there — no escrow hold to wait on).
        // A payout hiccup must never unwind the rent payment; the payout row is the recovery point.
        await _payoutService.CreateForPaidRentAsync(invoice);

        await _notificationService.CreateAsync(
            invoice.LandlordId,
            "Rent received",
            $"Rent of GHS {invoice.Amount:0.00} for {invoice.PeriodStart:dd MMM} – {invoice.PeriodEnd:dd MMM yyyy} was paid.",
            invoice.BookingId, "Booking");

        _logger.LogInformation("Rent invoice {InvoiceId} paid (ref {Reference}); landlord payout created", invoiceId, reference);
    }

    public async Task CancelOutstandingAsync(string bookingId)
    {
        var outstanding = (await _invoiceRepository.FindAsync(i =>
                i.BookingId == bookingId && i.Status != RentInvoiceStatus.Paid && i.Status != RentInvoiceStatus.Cancelled))
            .ToList();
        foreach (var invoice in outstanding)
        {
            invoice.Status = RentInvoiceStatus.Cancelled;
            await _invoiceRepository.UpdateAsync(invoice);
        }
        if (outstanding.Count > 0)
            _logger.LogInformation("Voided {Count} outstanding rent invoice(s) for cancelled booking {BookingId}",
                outstanding.Count, bookingId);
    }

    public async Task ProcessDueAndOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Upcoming → Due when inside the reminder window (tenant nudge).
        var dueCutoff = now.AddDays(ReminderDays);
        var toDue = await _invoiceRepository.FindAsync(i =>
            i.Status == RentInvoiceStatus.Upcoming && i.DueDate <= dueCutoff);
        foreach (var invoice in toDue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            invoice.Status = RentInvoiceStatus.Due;
            await _invoiceRepository.UpdateAsync(invoice);
            await _notificationService.CreateAsync(
                invoice.TenantId,
                "Rent due soon",
                $"Your rent of GHS {invoice.Amount:0.00} for {invoice.PeriodStart:dd MMM} – {invoice.PeriodEnd:dd MMM yyyy} is due on {invoice.DueDate:dd MMM}.",
                invoice.BookingId, "Booking");
        }

        // Due → Overdue once the due date passes (both parties told).
        var toOverdue = await _invoiceRepository.FindAsync(i =>
            (i.Status == RentInvoiceStatus.Due || i.Status == RentInvoiceStatus.Upcoming) && i.DueDate < now.Date);
        foreach (var invoice in toOverdue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            invoice.Status = RentInvoiceStatus.Overdue;
            await _invoiceRepository.UpdateAsync(invoice);
            await _notificationService.CreateAsync(
                invoice.TenantId,
                "Rent overdue",
                $"Your rent of GHS {invoice.Amount:0.00} due {invoice.DueDate:dd MMM yyyy} is overdue — please pay to keep your tenancy in good standing.",
                invoice.BookingId, "Booking");
            await _notificationService.CreateAsync(
                invoice.LandlordId,
                "Rent overdue",
                $"Rent of GHS {invoice.Amount:0.00} due {invoice.DueDate:dd MMM yyyy} has not been paid.",
                invoice.BookingId, "Booking");
        }

        if (toDue.Any() || toOverdue.Any())
        {
            await _invoiceRepository.SaveChangesAsync();
            _logger.LogInformation("Rent sweep: {Due} invoice(s) marked due, {Overdue} overdue",
                toDue.Count(), toOverdue.Count());
        }
    }

    /// <summary>One batched query for the bookings behind a set of invoices (bookingId → propertyId).</summary>
    private async Task<Dictionary<string, string>> PropertyIdsForAsync(IEnumerable<RentInvoice> invoices)
    {
        var bookingIds = invoices.Select(i => i.BookingId).Distinct().ToList();
        if (bookingIds.Count == 0)
            return new Dictionary<string, string>();
        return (await _bookingRepository.FindAsync(b => bookingIds.Contains(b.Id)))
            .ToDictionary(b => b.Id, b => b.PropertyId);
    }

    private static RentInvoiceResponse Map(RentInvoice invoice, string propertyId) => new()
    {
        InvoiceId = invoice.Id,
        BookingId = invoice.BookingId,
        PropertyId = propertyId,
        PeriodStart = invoice.PeriodStart,
        PeriodEnd = invoice.PeriodEnd,
        Amount = invoice.Amount,
        DueDate = invoice.DueDate,
        Status = invoice.Status,
        PaidAt = invoice.PaidAt
    };
}
