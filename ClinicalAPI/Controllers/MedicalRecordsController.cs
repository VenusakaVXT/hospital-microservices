using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicalAPI.Models;
using ClinicalAPI.Data;
using ClinicalAPI.Services;

namespace ClinicalAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MedicalRecordsController : ControllerBase
{
    private readonly ClinicalDbContext _db;
    private readonly ElasticSearchService _elasticService;
    private readonly ILogger<MedicalRecordsController> _logger;

    public MedicalRecordsController(ClinicalDbContext db, ElasticSearchService elasticService, ILogger<MedicalRecordsController> logger)
    {
        _db = db;
        _elasticService = elasticService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MedicalRecord>>> GetAll(
        [FromQuery] string? patientName,
        [FromQuery] string? status,
        [FromQuery] string? doctorName,
        CancellationToken cancellationToken)
    {
        var query = _db.MedicalRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(patientName))
            query = query.Where(r => r.PatientName.Contains(patientName));
            
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);
            
        if (!string.IsNullOrWhiteSpace(doctorName))
            query = query.Where(r => r.DoctorName != null && r.DoctorName.Contains(doctorName));

        var records = await query.OrderByDescending(r => r.VisitDate).ToListAsync(cancellationToken);
        return Ok(records);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<MedicalRecord>>> Search([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return BadRequest("Keyword is required");
        var results = await _elasticService.SearchMedicalRecordsAsync(keyword);
        return Ok(results);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MedicalRecord>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var record = await _db.MedicalRecords.FindAsync(new object[] { id }, cancellationToken);
        if (record == null) return NotFound();
        return Ok(record);
    }

    [HttpPost]
    public async Task<ActionResult<MedicalRecord>> Create([FromBody] MedicalRecord record, CancellationToken cancellationToken)
    {
        _db.MedicalRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        // Đồng bộ lên Elasticsearch
        await _elasticService.IndexMedicalRecordAsync(record);

        return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MedicalRecord record, CancellationToken cancellationToken)
    {
        if (id != record.Id) return BadRequest();

        _db.Entry(record).State = EntityState.Modified;
        
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            // Đồng bộ cập nhật lên Elasticsearch
            await _elasticService.IndexMedicalRecordAsync(record);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.MedicalRecords.AnyAsync(e => e.Id == id, cancellationToken))
                return NotFound();
            else
                throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var record = await _db.MedicalRecords.FindAsync(new object[] { id }, cancellationToken);
        if (record == null) return NotFound();

        _db.MedicalRecords.Remove(record);
        await _db.SaveChangesAsync(cancellationToken);

        // Xóa khỏi Elasticsearch
        await _elasticService.DeleteMedicalRecordAsync(id);

        return NoContent();
    }

    /// <summary>
    /// Đồng bộ toàn bộ dữ liệu từ Oracle DB lên Elasticsearch.
    /// Rất hữu ích khi insert dữ liệu bằng lệnh SQL thuần.
    /// </summary>
    [HttpPost("sync-all")]
    public async Task<IActionResult> SyncAllToElastic(CancellationToken cancellationToken)
    {
        var allRecords = await _db.MedicalRecords.ToListAsync(cancellationToken);
        int count = 0;
        foreach (var record in allRecords)
        {
            await _elasticService.IndexMedicalRecordAsync(record);
            count++;
        }
        return Ok(new { message = $"Đã đồng bộ thành công {count} hồ sơ bệnh án lên Elasticsearch!" });
    }
}
