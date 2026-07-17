using System.Text.Json;
using StackExchange.Redis;
using ClinicalAPI.Data;
using ClinicalAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicalAPI.Services;

/// <summary>
/// Service quản lý danh mục thuốc với cơ chế Cache-Aside bằng Redis.
/// </summary>
public class MedicineService
{
    private readonly ClinicalDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<MedicineService> _logger;
    
    // Key cố định dùng trong Redis cho danh sách thuốc hoạt động
    private const string CacheKey = "hospital:medicines";
    
    // Sử dụng SemaphoreSlim để khóa bất đồng bộ, chống Cache Stampede
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    
    public MedicineService(
        ClinicalDbContext db, 
        IConnectionMultiplexer redis, 
        ILogger<MedicineService> logger)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách thuốc đang hoạt động.
    /// Áp dụng Cache-Aside:
    /// 1. Đọc từ Redis.
    /// 2. Nếu miss (chưa có hoặc hết hạn), dùng lock để tránh Cache Stampede.
    /// 3. Truy vấn Oracle DB.
    /// 4. Lưu lại vào Redis với TTL = 30 phút.
    /// NẾU REDIS SẬP: Bypass hoàn toàn Redis và query thẳng xuống Oracle.
    /// </summary>
    public async Task<List<Medicine>> GetActiveMedicinesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dbRedis = _redis.GetDatabase();
            
            // 1. Thử lấy từ Cache trước
            var cachedData = await dbRedis.StringGetAsync(CacheKey);
            if (!cachedData.IsNullOrEmpty)
            {
                _logger.LogInformation("✅ [CACHE HIT] Loaded active medicines from Redis.");
                return JsonSerializer.Deserialize<List<Medicine>>(cachedData!)!;
            }
        }
        catch (RedisConnectionException ex)
        {
            // BẮT BUỘC: Resilience - Nếu Redis chết/bận, log cảnh báo và đi tiếp xuống DB
            _logger.LogWarning(ex, "⚠️ [REDIS OFFLINE] Redis is unavailable. Bypassing cache to query Oracle directly.");
            return await GetFromDatabaseAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error while accessing Redis. Bypassing cache.");
            return await GetFromDatabaseAsync(cancellationToken);
        }

        // 2. Cache Miss -> Lấy từ DB nhưng phải chống Cache Stampede
        _logger.LogInformation("❌ [CACHE MISS] Data not found in Redis. Preparing to query Oracle DB.");
        
        // Chờ đến lượt vào khối Critical Section (chống nhiều request cùng lúc chọc DB)
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // (Double-Check Locking) Sau khi vào được lock, phải kiểm tra lại xem có thread nào vừa fill cache không
            try
            {
                var dbRedis = _redis.GetDatabase();
                var doubleCheckCache = await dbRedis.StringGetAsync(CacheKey);
                if (!doubleCheckCache.IsNullOrEmpty)
                {
                    _logger.LogInformation("✅ [CACHE HIT AFTER LOCK] Another thread filled the cache.");
                    return JsonSerializer.Deserialize<List<Medicine>>(doubleCheckCache!)!;
                }
            }
            catch (Exception ex)
            {
                 // Bỏ qua lỗi kết nối Redis trong lock
                 _logger.LogWarning(ex, "⚠️ Redis error during double-check lock.");
            }

            // Thực sự lấy từ Oracle DB
            var medicines = await GetFromDatabaseAsync(cancellationToken);

            // Nạp lại vào Cache
            try
            {
                var dbRedis = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(medicines);
                // TTL = 30 phút
                await dbRedis.StringSetAsync(CacheKey, json, TimeSpan.FromMinutes(30));
                _logger.LogInformation("📝 [CACHE FILL] Successfully cached medicines to Redis with 30m TTL.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to save to Redis after querying DB.");
            }

            return medicines;
        }
        finally
        {
            // Luôn luôn nhả lock
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Helper truy vấn trực tiếp từ Oracle DB
    /// </summary>
    private async Task<List<Medicine>> GetFromDatabaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔍 Querying Oracle DB for active medicines...");
        return await _db.Medicines
            .Where(m => m.IsActive)
            .OrderBy(m => m.TradeName)
            .ToListAsync(cancellationToken);
    }
}
