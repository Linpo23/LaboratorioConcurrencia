using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioConcurrencia.Controllers;

[ApiController]
[Route("api/dashboard")]
[AllowAnonymous] // Permite hacer las pruebas de concurrencia de forma fluida
public class DashboardController : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        await Task.Delay(300);
        return Ok(new { status = "OK", module = "users" });
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        await Task.Delay(200);
        return Ok(new { status = "OK", module = "profile" });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        await Task.Delay(400);
        return Ok(new { status = "OK", module = "stats" });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        await Task.Delay(300);
        return Ok(new { status = "OK", module = "notifications" });
    }
}