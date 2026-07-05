using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Paystack payment gateway (test or live mode). Talks to https://api.paystack.co for
/// transaction initialize / verify / refund. If no secret key is configured it degrades
/// gracefully — returning a simulated reference + checkout URL and logging — so dev flows
/// work without credentials. Amounts are sent in the minor unit (pesewas = GHS * 100).
/// </summary>
public class PaystackPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaystackPaymentGateway> _logger;
    private readonly string? _secretKey;
    private readonly string? _callbackUrl;

    public PaystackPaymentGateway(HttpClient httpClient, IConfiguration configuration, ILogger<PaystackPaymentGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _secretKey = configuration["PaystackSettings:SecretKey"];
        _callbackUrl = configuration["PaystackSettings:CallbackUrl"];

        _httpClient.BaseAddress ??= new Uri("https://api.paystack.co/");
        if (!string.IsNullOrWhiteSpace(_secretKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
    }

    private bool Configured => !string.IsNullOrWhiteSpace(_secretKey);

    public async Task<PaymentInitResult> InitiatePaymentAsync(decimal amount, string currency, string customerEmail, string bookingId, string? callbackUrl = null)
    {
        var reference = $"TN-{bookingId[..Math.Min(8, bookingId.Length)]}-{Guid.NewGuid():N}";

        if (!Configured)
        {
            _logger.LogInformation("[Paystack not configured] simulating checkout for booking {BookingId}, ref {Reference}", bookingId, reference);
            return new PaymentInitResult(true, $"https://checkout.paystack.test/simulated/{reference}", reference);
        }

        try
        {
            var payload = new
            {
                email = customerEmail,
                amount = (long)(amount * 100), // pesewas
                currency,
                reference,
                callback_url = callbackUrl ?? _callbackUrl,
                metadata = new { bookingId }
            };
            using var resp = await _httpClient.PostAsync("transaction/initialize", JsonBody(payload));
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack initialize failed ({Status}): {Body}", resp.StatusCode, json);
                return new PaymentInitResult(false, string.Empty, reference);
            }

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var checkoutUrl = data.GetProperty("authorization_url").GetString() ?? string.Empty;
            var returnedRef = data.GetProperty("reference").GetString() ?? reference;
            return new PaymentInitResult(true, checkoutUrl, returnedRef);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack initialize error for booking {BookingId}", bookingId);
            return new PaymentInitResult(false, string.Empty, reference);
        }
    }

    public async Task<PaymentVerifyResult> VerifyPaymentAsync(string reference)
    {
        if (!Configured)
        {
            _logger.LogInformation("[Paystack not configured] simulating verify success for ref {Reference}", reference);
            return new PaymentVerifyResult(true, 0m);
        }

        try
        {
            using var resp = await _httpClient.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}");
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack verify failed ({Status}): {Body}", resp.StatusCode, json);
                return new PaymentVerifyResult(false, 0m);
            }

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var success = data.GetProperty("status").GetString() == "success";
            var amount = data.TryGetProperty("amount", out var a) ? a.GetDecimal() / 100m : 0m;
            return new PaymentVerifyResult(success, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack verify error for ref {Reference}", reference);
            return new PaymentVerifyResult(false, 0m);
        }
    }

    public async Task<bool> RefundAsync(string reference, decimal amount)
    {
        if (!Configured)
        {
            _logger.LogInformation("[Paystack not configured] simulating refund of {Amount} for ref {Reference}", amount, reference);
            return true;
        }

        try
        {
            var payload = new { transaction = reference, amount = (long)(amount * 100) };
            using var resp = await _httpClient.PostAsync("refund", JsonBody(payload));
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack refund failed ({Status}): {Body}", resp.StatusCode, await resp.Content.ReadAsStringAsync());
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack refund error for ref {Reference}", reference);
            return false;
        }
    }

    public async Task<TransferRecipientResult> CreateTransferRecipientAsync(
        string accountName, string accountNumber, string providerCode, string channel, string currency)
    {
        if (!Configured)
        {
            _logger.LogInformation("[Paystack not configured] simulating transfer recipient for {AccountName}", accountName);
            return new TransferRecipientResult(true, $"RCP_SIM_{Guid.NewGuid():N}", null);
        }

        try
        {
            // Ghana: MoMo wallets use type "mobile_money" (codes MTN/ATL/VOD); banks use "ghipss".
            var payload = new
            {
                type = channel,
                name = accountName,
                account_number = accountNumber,
                bank_code = providerCode,
                currency
            };
            using var resp = await _httpClient.PostAsync("transferrecipient", JsonBody(payload));
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack create-recipient failed ({Status}): {Body}", resp.StatusCode, json);
                return new TransferRecipientResult(false, null, ExtractMessage(json) ?? "The payout account was rejected by the payment provider.");
            }

            using var doc = JsonDocument.Parse(json);
            var code = doc.RootElement.GetProperty("data").GetProperty("recipient_code").GetString();
            return new TransferRecipientResult(!string.IsNullOrEmpty(code), code, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack create-recipient error for {AccountName}", accountName);
            return new TransferRecipientResult(false, null, "Could not reach the payment provider. Please retry.");
        }
    }

    public async Task<TransferResult> InitiateTransferAsync(
        decimal amount, string currency, string recipientCode, string reference, string reason)
    {
        if (!Configured)
        {
            _logger.LogInformation("[Paystack not configured] simulating transfer of {Amount} {Currency}, ref {Reference}", amount, currency, reference);
            return new TransferResult(true, $"TRF_SIM_{Guid.NewGuid():N}", "success", null);
        }

        try
        {
            var payload = new
            {
                source = "balance",
                amount = (long)(amount * 100), // pesewas
                currency,
                recipient = recipientCode,
                reference,
                reason
            };
            using var resp = await _httpClient.PostAsync("transfer", JsonBody(payload));
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack transfer failed ({Status}): {Body}", resp.StatusCode, json);
                return new TransferResult(false, null, null, ExtractMessage(json) ?? "The transfer was rejected by the payment provider.");
            }

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var transferCode = data.TryGetProperty("transfer_code", out var tc) ? tc.GetString() : null;
            var status = data.TryGetProperty("status", out var st) ? st.GetString() : null;
            return new TransferResult(true, transferCode, status, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack transfer error for ref {Reference}", reference);
            return new TransferResult(false, null, null, "Could not reach the payment provider. Please retry.");
        }
    }

    /// <summary>Pulls Paystack's human-readable "message" out of an error body, if present.</summary>
    private static string? ExtractMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
}
