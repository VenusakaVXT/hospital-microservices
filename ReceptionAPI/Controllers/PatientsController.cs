using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceptionAPI.DTOs;
using ReceptionAPI.Models;
using ReceptionAPI.Services;
using ReceptionAPI.Data;

namespace ReceptionAPI.Controllers;

/// <summary>
/// API endpoint quản lý đăng ký bệnh nhân tại quầy tiếp đón.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;
    private readonly ReceptionDbContext _db;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(IPatientService patientService, ReceptionDbContext db, ILogger<PatientsController> logger)
    {
        _patientService = patientService;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Patient>>> GetAll(
        [FromQuery] string? fullName,
        [FromQuery] string? gender,
        [FromQuery] string? nationalId,
        [FromQuery] string? phoneNumber,
        CancellationToken cancellationToken)
    {
        var query = _db.Patients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(fullName))
            query = query.Where(p => p.FullName.Contains(fullName));
        
        if (!string.IsNullOrWhiteSpace(gender))
            query = query.Where(p => p.Gender == gender);
            
        if (!string.IsNullOrWhiteSpace(nationalId))
            query = query.Where(p => p.NationalId == nationalId);
            
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            query = query.Where(p => p.PhoneNumber == phoneNumber);

        var patients = await query.OrderByDescending(p => p.RegisteredAt).ToListAsync(cancellationToken);
        return Ok(patients);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Patient>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var patient = await _db.Patients.FindAsync(new object[] { id }, cancellationToken);
        if (patient == null) return NotFound();
        return Ok(patient);
    }

    /// <summary>
    /// Đăng ký bệnh nhân mới tại quầy tiếp đón.
    /// Lưu thông tin vào bảng Patients và tạo OutboxMessage trong cùng 1 Transaction.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(PatientRegisteredResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RegisterPatient([FromBody] PatientDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _patientService.RegisterPatientAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.PatientId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error registering patient '{FullName}'.", dto.FullName);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau." });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PatientDto dto, CancellationToken cancellationToken)
    {
        var patient = await _db.Patients.FindAsync(new object[] { id }, cancellationToken);
        if (patient == null) return NotFound();

        patient.FullName = dto.FullName;
        patient.DateOfBirth = dto.DateOfBirth;
        patient.Gender = dto.Gender;
        patient.NationalId = dto.NationalId;
        patient.PhoneNumber = dto.PhoneNumber;
        patient.Address = dto.Address;
        patient.InsuranceType = dto.InsuranceType;
        patient.InsuranceNumber = dto.InsuranceNumber;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var patient = await _db.Patients.FindAsync(new object[] { id }, cancellationToken);
        if (patient == null) return NotFound();

        _db.Patients.Remove(patient);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
