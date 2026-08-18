using DevGuard.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevGuard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScanController : ControllerBase
{
    private readonly ScanPipelineService _pipelineService;

    public ScanController(ScanPipelineService pipelineService)
    {
        _pipelineService = pipelineService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartScan([FromBody] ScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FolderPath) || !Directory.Exists(request.FolderPath))
        {
            return BadRequest(new { message = "Thư mục không tồn tại hoặc đường dẫn không hợp lệ." });
        }

        try
        {
            var result = await _pipelineService.ExecuteScanAsync(request.FolderPath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi hệ thống: {ex.Message}" });
        }
    }
}

public class ScanRequest
{
    public string FolderPath { get; set; } = string.Empty;
}