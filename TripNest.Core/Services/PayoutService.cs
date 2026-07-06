using Microsoft.Extensions.Options;
using TripNest.Core.DTOs.Payouts;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Options;

namespace TripNest.Core.Services;

public class PayoutService : IPayoutService
{
    private readonly IRepository<Payout> _payoutRepository;
    private readonly IRepository<PayoutAccount> _accountRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly INotificationService _notificationService;
    private readonly PlatformOptions _platform;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(
        IRepository<Payout> payoutRepository,
        IRepository<PayoutAccount> accountRepository,
        IPaymentGateway paymentGateway,
        INotificationService notificationService,
        IOptions<PlatformOptions> platformOptions,
        ILogger<PayoutService> logger)
    {
        _payoutRepository = payoutRepository;
        _accountRepository = accountRepository;
        _paymentGateway = paymentGateway;
        _notificationService = notificationService;
        _platform = platformOptions.Value;
        _logger = logger;
    }

    private static readonly string[] AllowedChannels = { "mobile_money", "ghipss" };

    public async Task<PayoutAccountResponse?> GetMyAccountAsync(string userId)
    {
        var account = (await _accountRepository.FindAsync(a => a.UserId == userId)).FirstOrDefault();
        return account is null ? null : MapAccount(account);
    }

    public async Task<PayoutAccountResponse> UpsertMyAccountAsync(string userId, UpsertPayoutAccountRequest request)
    {
        var channel = request.Channel?.Trim().ToLowerInvariant();
        if (channel is null || !AllowedChannels.Contains(channel))
            throw new ValidationException("Channel must be 'mobile_money' or 'ghipss'.");
        if (string.IsNullOrWhiteSpace(request.ProviderCode))
            throw new ValidationException("A provider code is required (e.g. MTN, ATL, VOD).");
        if (string.IsNullOrWhiteSpace(request.AccountNumber) || request.AccountNumber.Trim().Length < 8)
            throw new ValidationException("A valid account/wallet number is required.");
        if (string.IsNullOrWhiteSpace(request.AccountName))
            throw new ValidationException("The account holder's name is required.");

        // Register with the provider first: only persist a destination Paystack has accepted,
        // so a payout can never be sent to an unregistered account.
        var recipient = await _paymentGateway.CreateTransferRecipientAsync(
            request.AccountName.Trim(), request.AccountNumber.Trim(), request.ProviderCode.Trim().ToUpperInvariant(),
            channel, _platform.Currency);
        if (!recipient.Success)
            throw new ValidationException(recipient.Error ?? "The payout account was rejected by the payment provider.");

        var account = (await _accountRepository.FindAsync(a => a.UserId == userId)).FirstOrDefault();
        if (account is null)
        {
            account = new PayoutAccount
            {
                UserId = userId,
                Channel = channel,
                ProviderCode = request.ProviderCode.Trim().ToUpperInvariant(),
                AccountNumber = request.AccountNumber.Trim(),
                AccountName = request.AccountName.Trim(),
                RecipientCode = recipient.RecipientCode
            };
            await _accountRepository.AddAsync(account);
        }
        else
        {
            account.Channel = channel;
            account.ProviderCode = request.ProviderCode.Trim().ToUpperInvariant();
            account.AccountNumber = request.AccountNumber.Trim();
            account.AccountName = request.AccountName.Trim();
            account.RecipientCode = recipient.RecipientCode;
            account.UpdatedAt = DateTime.UtcNow;
            await _accountRepository.UpdateAsync(account);
        }
        await _accountRepository.SaveChangesAsync();

        _logger.LogInformation("Payout account registered for user {UserId} ({Channel}/{Provider})",
            userId, channel, account.ProviderCode);
        return MapAccount(account);
    }

    public async Task<List<PayoutResponse>> GetMyPayoutsAsync(string userId)
    {
        var payouts = await _payoutRepository.FindAsync(p => p.LandlordId == userId);
        return payouts.OrderByDescending(p => p.CreatedAt).Select(Map).ToList();
    }

    public async Task CreateForReleasedEscrowAsync(Escrow escrow, string landlordId, decimal? grossOverride = null)
    {
        try
        {
            // Idempotent: at most one payout per escrow (backed by a unique index).
            var existing = (await _payoutRepository.FindAsync(p => p.EscrowId == escrow.Id)).FirstOrDefault();
            if (existing is not null)
                return;

            // Same fee source as statements and the reservation breakdown — the money that moves
            // must match the money the UI promised.
            var gross = grossOverride ?? escrow.Amount;
            var fee = Math.Round(gross * _platform.ManagementFeePercent / 100m, 2);

            var payout = new Payout
            {
                EscrowId = escrow.Id,
                BookingId = escrow.BookingId,
                LandlordId = landlordId,
                GrossAmount = gross,
                FeeAmount = fee,
                Amount = gross - fee,
                Status = PayoutStatus.Pending
            };
            await _payoutRepository.AddAsync(payout);
            await _payoutRepository.SaveChangesAsync();

            await AttemptTransferAsync(payout);
        }
        catch (Exception ex)
        {
            // Never let a payout hiccup undo or block the escrow release that triggered it —
            // the payout row (or its absence in logs) is the recovery point.
            _logger.LogError(ex, "Failed to create payout for escrow {EscrowId}", escrow.Id);
        }
    }

    public async Task<PayoutResponse> RetryAsync(string payoutId, string userId)
    {
        var payout = await _payoutRepository.GetByIdAsync(payoutId)
            ?? throw new NotFoundException("Payout");
        if (payout.LandlordId != userId)
            throw new ForbiddenException("This payout is not yours");
        if (payout.Status is not (PayoutStatus.Pending or PayoutStatus.Failed))
            throw new ValidationException($"A payout in status '{payout.Status}' cannot be retried.");

        await AttemptTransferAsync(payout);

        if (payout.Status == PayoutStatus.Pending)
            throw new ValidationException("Add a payout account first, then retry.");

        return Map(payout);
    }

    public async Task HandleTransferWebhookAsync(string eventType, string reference, string? failureReason)
    {
        // The transfer reference is the payout id.
        var payout = await _payoutRepository.GetByIdAsync(reference);
        if (payout is null)
        {
            _logger.LogWarning("Transfer webhook {Event} for unknown reference {Reference}", eventType, reference);
            return;
        }

        switch (eventType)
        {
            case "transfer.success":
                // Idempotent: repeated success webhooks are a no-op.
                if (payout.Status == PayoutStatus.Paid)
                    return;
                payout.Status = PayoutStatus.Paid;
                payout.PaidAt = DateTime.UtcNow;
                payout.FailureReason = null;
                await _payoutRepository.UpdateAsync(payout);
                await _payoutRepository.SaveChangesAsync();
                await _notificationService.NotifyAsync(payout.LandlordId, NotificationType.PaymentReceived,
                    "Payout sent",
                    $"GH₵{payout.Amount:0.00} for booking {payout.BookingId} has been sent to your payout account.");
                break;

            case "transfer.failed":
            case "transfer.reversed":
                payout.Status = PayoutStatus.Failed;
                payout.FailureReason = failureReason ?? eventType;
                await _payoutRepository.UpdateAsync(payout);
                await _payoutRepository.SaveChangesAsync();
                await _notificationService.NotifyAsync(payout.LandlordId, NotificationType.General,
                    "Payout failed",
                    $"Your GH₵{payout.Amount:0.00} payout could not be delivered. Check your payout account and retry from the payouts page.");
                break;

            default:
                _logger.LogInformation("Ignoring transfer webhook event {Event} for payout {PayoutId}", eventType, payout.Id);
                break;
        }
    }

    /// <summary>
    /// Initiates the provider transfer for a payout when the host has a registered account.
    /// Mutates + persists the payout's status; swallows nothing — callers decide how to react
    /// to the resulting state (Pending = no account, Processing = sent, Failed = rejected).
    /// </summary>
    private async Task AttemptTransferAsync(Payout payout)
    {
        var account = (await _accountRepository.FindAsync(a => a.UserId == payout.LandlordId)).FirstOrDefault();
        if (account?.RecipientCode is null)
        {
            _logger.LogInformation("Payout {PayoutId} waiting: landlord {LandlordId} has no payout account",
                payout.Id, payout.LandlordId);
            await _notificationService.NotifyAsync(payout.LandlordId, NotificationType.General,
                "Add a payout account",
                $"GH₵{payout.Amount:0.00} is ready to be paid out. Add your Mobile Money or bank details to receive it.");
            return;
        }

        var result = await _paymentGateway.InitiateTransferAsync(
            payout.Amount, _platform.Currency, account.RecipientCode, payout.Id,
            $"TripNest payout for booking {payout.BookingId}");

        if (result.Success)
        {
            payout.TransferCode = result.TransferCode;
            payout.FailureReason = null;
            // Test-mode (and the simulated gateway) can report success synchronously; otherwise the
            // webhook confirms. Either way the money is on its way.
            if (string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                payout.Status = PayoutStatus.Paid;
                payout.PaidAt = DateTime.UtcNow;
            }
            else
            {
                payout.Status = PayoutStatus.Processing;
            }
        }
        else
        {
            payout.Status = PayoutStatus.Failed;
            payout.FailureReason = result.Error;
        }

        await _payoutRepository.UpdateAsync(payout);
        await _payoutRepository.SaveChangesAsync();

        _logger.LogInformation("Payout {PayoutId} transfer attempt → {Status} ({TransferCode})",
            payout.Id, payout.Status, payout.TransferCode ?? "-");
    }

    private static PayoutAccountResponse MapAccount(PayoutAccount a) => new()
    {
        Channel = a.Channel,
        ProviderCode = a.ProviderCode,
        AccountNumber = Mask(a.AccountNumber),
        AccountName = a.AccountName,
        ProviderRegistered = !string.IsNullOrEmpty(a.RecipientCode),
        UpdatedAt = a.UpdatedAt
    };

    private static string Mask(string accountNumber) =>
        accountNumber.Length <= 3 ? "***" : new string('*', accountNumber.Length - 3) + accountNumber[^3..];

    private static PayoutResponse Map(Payout p) => new()
    {
        PayoutId = p.Id,
        EscrowId = p.EscrowId,
        BookingId = p.BookingId,
        GrossAmount = p.GrossAmount,
        FeeAmount = p.FeeAmount,
        Amount = p.Amount,
        Status = p.Status,
        FailureReason = p.FailureReason,
        CreatedAt = p.CreatedAt,
        PaidAt = p.PaidAt
    };
}
