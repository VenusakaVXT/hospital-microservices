using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicalAPI.Services;
using ClinicalAPI.Models;
using ClinicalAPI.Data;
using StackExchange.Redis;

namespace ClinicalAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MedicinesController : ControllerBase
{
    private readonly MedicineService _medicineService;
    private readonly ClinicalDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<MedicinesController> _logger;

    public MedicinesController(
        MedicineService medicineService, 
        ClinicalDbContext db, 
        IConnectionMultiplexer redis, 
        ILogger<MedicinesController> logger)
    {
        _medicineService = medicineService;
        _db = db;
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách thuốc đang hoạt động (có áp dụng Redis Cache).
    /// Áp dụng filter in-memory trên tập kết quả từ cache để giữ tốc độ siêu nhanh.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Medicine>>> GetAll(
        [FromQuery] string? tradeName,
        [FromQuery] string? genericName,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Nhận request lấy danh sách thuốc...");
        
        // 1. Lấy toàn bộ danh sách active từ Cache (hoặc DB nếu Miss)
        var medicines = await _medicineService.GetActiveMedicinesAsync(cancellationToken);
        
        // 2. Filter trên Memory (rất nhanh vì data đã load lên RAM)
        var query = medicines.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(tradeName))
            query = query.Where(m => m.TradeName.Contains(tradeName, StringComparison.OrdinalIgnoreCase));
            
        if (!string.IsNullOrWhiteSpace(genericName))
            query = query.Where(m => m.GenericName.Contains(genericName, StringComparison.OrdinalIgnoreCase));
            
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(m => m.Category != null && m.Category.Contains(category, StringComparison.OrdinalIgnoreCase));

        return Ok(query.ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Medicine>> GetById(int id, CancellationToken cancellationToken)
    {
        var medicine = await _db.Medicines.FindAsync(new object[] { id }, cancellationToken);
        if (medicine == null) return NotFound();
        return Ok(medicine);
    }

    [HttpPost]
    public async Task<ActionResult<Medicine>> Create([FromBody] Medicine medicine, CancellationToken cancellationToken)
    {
        _db.Medicines.Add(medicine);
        await _db.SaveChangesAsync(cancellationToken);

        // Xóa Cache để lần GET tiếp theo lấy data mới
        await InvalidateCacheAsync();

        return CreatedAtAction(nameof(GetById), new { id = medicine.Id }, medicine);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Medicine medicine, CancellationToken cancellationToken)
    {
        if (id != medicine.Id) return BadRequest();

        _db.Entry(medicine).State = EntityState.Modified;
        
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            // Xóa Cache
            await InvalidateCacheAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Medicines.AnyAsync(e => e.Id == id, cancellationToken))
                return NotFound();
            else
                throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var medicine = await _db.Medicines.FindAsync(new object[] { id }, cancellationToken);
        if (medicine == null) return NotFound();

        _db.Medicines.Remove(medicine);
        await _db.SaveChangesAsync(cancellationToken);

        // Xóa Cache
        await InvalidateCacheAsync();

        return NoContent();
    }

    private async Task InvalidateCacheAsync()
    {
        try
        {
            await _redis.GetDatabase().KeyDeleteAsync("hospital:medicines");
            _logger.LogInformation("🧹 [CACHE INVALIDATED] Removed hospital:medicines key.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to invalidate cache.");
        }
    }
}
