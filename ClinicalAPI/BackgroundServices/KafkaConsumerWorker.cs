using Confluent.Kafka;
using System.Text.Json;
using ClinicalAPI.Data;
using ClinicalAPI.Models;
using ClinicalAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace ClinicalAPI.BackgroundServices;

/// <summary>
/// Worker chạy ngầm, liên tục lắng nghe topic 'hospital-patients' từ Kafka.
/// Nhận sự kiện bệnh nhân đăng ký từ ReceptionAPI và tạo hồ sơ bệnh án tự động.
/// </summary>
public class KafkaConsumerWorker : BackgroundService
{
    private readonly ILogger<KafkaConsumerWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    
    private const string TopicName = "hospital-patients";

    public KafkaConsumerWorker(
        ILogger<KafkaConsumerWorker> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Khởi chạy việc consume Kafka trên một luồng riêng để không block quá trình khởi động của ứng dụng
        _ = Task.Run(() => ConsumeKafkaAsync(stoppingToken), stoppingToken);
        return Task.CompletedTask;
    }

    private async Task ConsumeKafkaAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            GroupId = _configuration["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest, // Đọc từ message đầu tiên nếu chưa có commit offset
            EnableAutoCommit = false // Tắt tự động commit, ta sẽ commit thủ công sau khi xử lý xong (để đảm bảo không mất dữ liệu)
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        consumer.Subscribe(TopicName);

        _logger.LogInformation("🎧 KafkaConsumerWorker started. Listening to topic '{Topic}'.", TopicName);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Lấy message từ Kafka (sẽ block cho đến khi có message mới hoặc bị cancel)
                    var consumeResult = consumer.Consume(stoppingToken);
                    var messagePayload = consumeResult.Message.Value;

                    _logger.LogInformation("📥 [KAFKA] Received message: {Payload}", messagePayload);

                    // Xử lý message và lưu vào DB
                    await ProcessMessageAsync(messagePayload, stoppingToken);

                    // Commit offset thủ công sau khi xử lý DB thành công
                    consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "❌ Error consuming Kafka message.");
                    // Đợi 1 giây trước khi thử lại để tránh flood log khi topic chưa được tạo
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // App tắt
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("⛔ KafkaConsumerWorker stopped.");
        }
    }

    private async Task ProcessMessageAsync(string payload, CancellationToken stoppingToken)
    {
        // 1. Parse JSON payload
        using var jsonDoc = JsonDocument.Parse(payload);
        var root = jsonDoc.RootElement;

        var patientIdStr = root.GetProperty("PatientId").GetString();
        var fullName = root.GetProperty("FullName").GetString();

        if (string.IsNullOrEmpty(patientIdStr) || !Guid.TryParse(patientIdStr, out var patientId))
        {
            _logger.LogWarning("⚠️ Invalid PatientId in payload.");
            return;
        }

        // 2. Tạo scope để lấy ClinicalDbContext
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        var elasticService = scope.ServiceProvider.GetRequiredService<ElasticSearchService>();

        // Kiểm tra xem bệnh nhân đã có hồ sơ chờ chưa để tránh trùng lặp
        var existingRecord = await db.MedicalRecords
            .AnyAsync(m => m.PatientId == patientId && m.Status == "Pending", stoppingToken);

        if (existingRecord)
        {
            _logger.LogInformation("ℹ️ Patient '{FullName}' already has a pending medical record. Skipping.", fullName);
            return;
        }

        // 3. Tạo hồ sơ bệnh án mới
        var record = new MedicalRecord
        {
            PatientId = patientId,
            PatientName = fullName ?? "Unknown",
            Status = "Pending",
            VisitDate = DateTime.UtcNow,
            Notes = "Tự động tạo từ hệ thống Tiếp đón."
        };

        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync(stoppingToken);

        _logger.LogInformation("✅ Successfully created medical record for patient '{FullName}'.", record.PatientName);

        // Đồng bộ lên Elasticsearch
        await elasticService.IndexMedicalRecordAsync(record);
    }
}
