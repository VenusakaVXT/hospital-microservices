using Microsoft.EntityFrameworkCore;
using ReceptionAPI.Data;

namespace ReceptionAPI.BackgroundServices;

/// <summary>
/// Worker chạy ngầm, quét OutboxMessages chưa xử lý mỗi 5 giây.
///
/// THIẾT KẾ QUAN TRỌNG — Singleton vs Scoped:
///   BackgroundService được đăng ký là Singleton (tồn tại suốt vòng đời app).
///   Nhưng DbContext (ReceptionDbContext) là Scoped (tạo mới mỗi request/scope).
///   → KHÔNG inject DbContext trực tiếp vào constructor!
///   → Phải dùng IServiceScopeFactory để tạo scope mới mỗi lần cần DB.
///
/// LUỒNG HOẠT ĐỘNG (mỗi 5 giây):
///   1. Tạo scope mới → lấy DbContext từ scope
///   2. Query OutboxMessages có IsProcessed == false
///   3. Log thông tin từng message ra console (tạm thay cho Kafka publish)
///   4. Đánh dấu IsProcessed = true + ghi ProcessedAt
///   5. Lưu thay đổi vào DB
///   6. Dispose scope → chờ 5 giây → lặp lại
/// </summary>
public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    // Khoảng thời gian giữa các lần quét (có thể đưa vào config sau)
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Hàm chính — chạy liên tục cho đến khi app tắt (stoppingToken bị cancelled).
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "🚀 OutboxPublisherWorker started. Polling every {Interval} seconds.",
            PollingInterval.TotalSeconds);

        // Vòng lặp vô hạn — dừng khi stoppingToken bị cancel (app shutdown)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log lỗi nhưng KHÔNG crash worker — tiếp tục vòng lặp
                _logger.LogError(ex, "⚠️ Error occurred while processing outbox messages.");
            }

            // Chờ 5 giây trước lần quét tiếp theo
            // Task.Delay sẽ bị cancel ngay khi stoppingToken fired → app tắt sạch
            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("⛔ OutboxPublisherWorker stopped.");
    }

    /// <summary>
    /// Xử lý tất cả OutboxMessages chưa được publish trong 1 vòng quét.
    /// </summary>
    private async Task ProcessPendingMessagesAsync(CancellationToken stoppingToken)
    {
        // Tạo scope mới để lấy DbContext (Scoped service) đúng cách
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReceptionDbContext>();

        // Lấy danh sách message chưa xử lý, sắp xếp theo thời gian tạo
        var pendingMessages = await db.OutboxMessages
            .Where(m => !m.IsProcessed)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(stoppingToken);

        if (!pendingMessages.Any())
        {
            _logger.LogDebug("📭 No pending outbox messages found.");
            return;
        }

        _logger.LogInformation(
            "📬 Found {Count} pending outbox message(s). Processing...",
            pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            // ── BƯỚC 3: Log ra console (tạm thay cho Kafka publish ở Ngày 3+) ──
            _logger.LogInformation(
                "📤 [OUTBOX] Publishing message — " +
                "Id: {MessageId} | EventType: {EventType} | PatientId: {PatientId} | " +
                "CreatedAt: {CreatedAt:yyyy-MM-dd HH:mm:ss} | Payload: {Payload}",
                message.Id,
                message.EventType,
                message.PatientId,
                message.CreatedAt,
                message.Payload);

            // ── BƯỚC 4: Đánh dấu đã xử lý ─────────────────────────────────────
            message.IsProcessed  = true;
            message.ProcessedAt  = DateTime.UtcNow;
        }

        // ── BƯỚC 5: Lưu tất cả thay đổi trong 1 lần SaveChanges (tối ưu) ──────
        var savedCount = await db.SaveChangesAsync(stoppingToken);

        _logger.LogInformation(
            "✅ Successfully marked {Count} outbox message(s) as processed.",
            savedCount);
    }
}
