using Microsoft.AspNetCore.Mvc;
using ClinicalAPI.Services;
using ClinicalAPI.Models;

namespace ClinicalAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MedicinesController : ControllerBase
{
    private readonly MedicineService _medicineService;
    private readonly ILogger<MedicinesController> _logger;

    public MedicinesController(MedicineService medicineService, ILogger<MedicinesController> logger)
    {
        _medicineService = medicineService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách thuốc đang hoạt động (có áp dụng Redis Cache).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Medicine>>> GetActiveMedicines(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Nhận request lấy danh sách thuốc...");
        var medicines = await _medicineService.GetActiveMedicinesAsync(cancellationToken);
        return Ok(medicines);
    }
}
