using CryptoDash.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/coins")]
public class CoinsController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var coins = await db.Coins
            .Where(c => c.IsActive)
            .OrderBy(c => c.Symbol)
            .Select(c => new { c.Id, c.Symbol, c.Name, c.IconUrl, RiskTier = c.RiskTier.ToString() })
            .ToListAsync(ct);
        return Ok(coins);
    }
}
