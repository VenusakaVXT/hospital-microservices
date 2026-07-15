using Microsoft.AspNetCore.Mvc;
using ReceptionAPI.DTOs;
using ReceptionAPI.Services;

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
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(IPatientService patientService, ILogger<PatientsController> logger)
    {
        _patientService = patientService;
        _logger = logger;
    }

    /// <summary>
    /// Đăng ký bệnh nhân mới tại quầy tiếp đón.
    /// Lưu thông tin vào bảng Patients và tạo OutboxMessage trong cùng 1 Transaction.
    /// </summary>
    /// <param name="dto">Thông tin bệnh nhân</param>
    /// <returns>Thông tin bệnh nhân vừa đăng ký</returns>
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
            return CreatedAtAction(nameof(RegisterPatient), new { id = result.PatientId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error registering patient '{FullName}'.", dto.FullName);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau." });
        }
    }
}
