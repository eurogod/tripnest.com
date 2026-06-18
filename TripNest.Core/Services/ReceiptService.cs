using System.Text;
using TripNest.Core.DTOs.Receipts;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class ReceiptService : IReceiptService
{
    private readonly IReceiptRepository _receiptRepository;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(
        IReceiptRepository receiptRepository,
        ILogger<ReceiptService> logger)
    {
        _receiptRepository = receiptRepository;
        _logger = logger;
    }

    public async Task<PagedResult<ReceiptResponse>> GetUserReceiptsAsync(string userId, int page, int pageSize)
    {
        try
        {
            var all = await _receiptRepository.GetByUserIdAsync(userId);
            var list = all.ToList();
            var totalCount = list.Count;
            var items = list
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(Map)
                .ToList();

            return new PagedResult<ReceiptResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving receipts for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ReceiptResponse?> GetReceiptAsync(string receiptId, string userId)
    {
        try
        {
            var receipt = await _receiptRepository.GetByIdAsync(receiptId);
            if (receipt == null || receipt.UserId != userId)
                return null;

            return Map(receipt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving receipt {ReceiptId} for user {UserId}", receiptId, userId);
            throw;
        }
    }

    public async Task<ReceiptResponse?> GetReceiptByBookingAsync(string bookingId, string userId)
    {
        try
        {
            var receipts = await _receiptRepository.GetByBookingIdAsync(bookingId);
            var receipt = receipts.FirstOrDefault(r => r.UserId == userId);
            return receipt == null ? null : Map(receipt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving receipt for booking {BookingId} and user {UserId}", bookingId, userId);
            throw;
        }
    }

    public async Task<(byte[], string)> DownloadReceiptPdfAsync(string receiptId, string userId)
    {
        try
        {
            var receipt = await _receiptRepository.GetByIdAsync(receiptId);
            if (receipt == null || receipt.UserId != userId)
                throw new InvalidOperationException("Receipt not found");

            // Stub — real QuestPDF integration is a future task
            var content = $"TripNest Receipt\nBookingId:{receipt.BookingId}\nAmount:{receipt.Amount}\nDate:{receipt.CreatedAt}";
            var bytes = Encoding.UTF8.GetBytes(content);
            var filename = $"receipt-{receipt.Id}.txt";

            _logger.LogInformation("Receipt {ReceiptId} downloaded by user {UserId}", receiptId, userId);

            return (bytes, filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading receipt {ReceiptId}", receiptId);
            throw;
        }
    }

    private static ReceiptResponse Map(Receipt r) => new()
    {
        ReceiptId = r.Id,
        BookingId = r.BookingId,
        UserId = r.UserId,
        Amount = r.Amount,
        Description = r.Description,
        PaymentMethod = r.PaymentMethod,
        CreatedAt = r.CreatedAt
    };
}
