using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using ReceptionAPI.Data;
using System.Text.Json;

namespace ReceptionAPI.BackgroundServices;

/// <summary>
/// Worker chạy ngầm, quét OutboxMessages chưa xử lý mỗi 5 giây.
/// (Đã được cập nhật ở Ngày 3 để publish sự kiện lên Kafka)
/// </summary>
public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly IProducer<Null, string> _kafkaProducer;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const string TopicName = "hospital-patients";

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Cấu hình Kafka Producer
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            // Các thiết lập nâng cao cho Producer (tùy chọn)
            Acks = Acks.All, // Đảm bảo tất cả broker replicas đã nhận được message
            MessageSendMaxRetries = 3
        };

        _kafkaProducer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "🚀 OutboxPublisherWorker started. Polling every {Interval} seconds.",
            PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "⚠️ Error occurred while processing outbox messages.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("⛔ OutboxPublisherWorker stopped.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReceptionDbContext>();

        var pendingMessages = await db.OutboxMessages
            .Where(m => !m.IsProcessed)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(stoppingToken);

        if (!pendingMessages.Any()) return;

        _logger.LogInformation(
            "📬 Found {Count} pending outbox message(s). Processing...",
            pendingMessages.Count);

        int successCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                // Đẩy message lên Kafka
                var kafkaMessage = new Message<Null, string> { Value = message.Payload };
                
                var deliveryResult = await _kafkaProducer.ProduceAsync(TopicName, kafkaMessage, stoppingToken);

                _logger.LogInformation(
                    "📤 [KAFKA] Published message {MessageId} to topic {Topic} partition {Partition} @ offset {Offset}",
                    message.Id, deliveryResult.Topic, deliveryResult.Partition, deliveryResult.Offset);

                // Chỉ khi publish thành công mới đánh dấu IsProcessed = true
                message.IsProcessed = true;
                message.ProcessedAt = DateTime.UtcNow;
                successCount++;
            }
            catch (ProduceException<Null, string> ex)
            {
                _logger.LogError(ex, "❌ Failed to publish message {MessageId} to Kafka.", message.Id);
                // Dừng xử lý batch này, chờ vòng lặp sau thử lại
                break;
            }
        }

        if (successCount > 0)
        {
            // Lưu tất cả những message đã xử lý thành công
            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("✅ Successfully marked {Count} outbox message(s) as processed.", successCount);
        }
    }

    public override void Dispose()
    {
        // Đảm bảo flush các message chưa gửi hết và giải phóng tài nguyên
        _kafkaProducer.Flush(TimeSpan.FromSeconds(10));
        _kafkaProducer.Dispose();
        base.Dispose();
    }
}
