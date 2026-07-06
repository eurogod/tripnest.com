using System.Text.RegularExpressions;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

/// <summary>
/// Guards the platform's core promise — escrow-protected payments — by watching chat for
/// attempts to move payment off-platform ("send MoMo directly, skip the fee"). Two layers:
/// cheap always-on regex rules for recall, then an AI verdict (when configured) for precision
/// on the rare rule hits. A flag NEVER blocks the message — it warns the recipient in-app,
/// deduped to one safety tip per recipient per day. Failures are swallowed: safety advice must
/// never break chat.
/// </summary>
public class ScamDetectionService : IScamDetectionService
{
    private readonly IAiClient _aiClient;
    private readonly INotificationService _notificationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<ScamDetectionService> _logger;

    public ScamDetectionService(
        IAiClient aiClient,
        INotificationService notificationService,
        INotificationRepository notificationRepository,
        ILogger<ScamDetectionService> logger)
    {
        _aiClient = aiClient;
        _notificationService = notificationService;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    // Payment-ish language …
    private static readonly Regex PaymentTerms = new(
        @"\b(momo|mobile\s*money|mtn\s*momo|telecel\s*cash|at\s*money|pay|payment|send|transfer|deposit|cash|western\s*union|wire)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // … combined with moving off the platform.
    private static readonly Regex OffPlatformTerms = new(
        @"\b(directly|outside\s+the\s+(app|platform|site)|off\s+the\s+(app|platform|site)|skip\s+the\s+(fee|platform|app|escrow)|avoid\s+the\s+(fee|platform|escrow)|no\s+need\s+for\s+the\s+(app|platform|escrow)|before\s+booking|without\s+booking)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A phone/wallet number next to payment language is the classic "send it to this number".
    private static readonly Regex NumberWithPayment = new(
        @"(momo|mobile\s*money|send|pay|transfer|deposit).{0,40}(\+233\d{9}|\b0\d{9}\b)|(\+233\d{9}|\b0\d{9}\b).{0,40}(momo|mobile\s*money|send|pay|transfer|deposit)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>True when the cheap rule layer thinks the message tries to move payment off-platform.</summary>
    internal static bool RulesFlag(string content) =>
        NumberWithPayment.IsMatch(content) ||
        (PaymentTerms.IsMatch(content) && OffPlatformTerms.IsMatch(content));

    public async Task ScanMessageAsync(Message message, Conversation conversation)
    {
        try
        {
            if (!RulesFlag(message.Content))
                return;

            // Precision layer: let the model veto rule false-positives ("I'll pay directly
            // through the app" is fine). If AI is unconfigured or fails, trust the rules.
            if (_aiClient.IsConfigured)
            {
                var raw = await _aiClient.CompleteAsync(VerdictSystemPrompt,
                    $"Message from one user to another on the platform:\n\"{message.Content}\"");
                var verdict = AiJson.TryParse<ScamVerdict>(raw);
                if (verdict is { Scam: false })
                {
                    _logger.LogInformation("Scam rules flagged message {MessageId} but AI cleared it", message.Id);
                    return;
                }
            }

            var recipientId = conversation.User1Id == message.SenderId ? conversation.User2Id : conversation.User1Id;
            _logger.LogWarning(
                "Possible off-platform payment attempt in conversation {ConversationId} (message {MessageId}, sender {SenderId})",
                conversation.Id, message.Id, message.SenderId);

            // One safety tip per recipient per day — a persistent scammer shouldn't turn the
            // warning into notification spam.
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var alreadyWarned = (await _notificationRepository.FindAsync(n =>
                n.UserId == recipientId && n.Type == NotificationType.SafetyAlert && n.CreatedAt > cutoff))
                .Any(n => n.Title.Contains("payments on TripNest"));
            if (alreadyWarned)
                return;

            await _notificationService.NotifyAsync(recipientId, NotificationType.SafetyAlert,
                "Safety tip: keep payments on TripNest",
                "A recent chat message mentioned paying outside the platform. Payments made outside " +
                "TripNest are NOT protected by escrow — if something goes wrong, we cannot refund you. " +
                "Always book and pay through the app.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scam scan failed for message {MessageId} — message delivery unaffected", message.Id);
        }
    }

    private const string VerdictSystemPrompt =
        """
        You review chat messages on TripNest, an accommodation-booking platform in Ghana where
        payments must go through the platform's escrow. Decide whether the message is attempting
        to move PAYMENT outside the platform (e.g. asking to send Mobile Money directly to a
        number, paying cash to skip the booking fee, wiring money before booking).
        Ordinary conversation, questions about the platform's own payment flow, or discussing
        prices is NOT a scam attempt.
        Reply ONLY with JSON: {"scam": true|false, "reason": "<one short sentence>"}
        """;

    private sealed class ScamVerdict
    {
        public bool Scam { get; set; }
        public string? Reason { get; set; }
    }
}
