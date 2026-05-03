using CryptoDash.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoDash.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public IActionResult Live() => Ok(new { status = "ok", at = DateTimeOffset.UtcNow });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        return dbOk ? Ok(new { status = "ready", db = true }) : StatusCode(503, new { status = "not-ready", db = false });
    }
}
