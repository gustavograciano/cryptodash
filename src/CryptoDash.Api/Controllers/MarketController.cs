using CryptoDash.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoDash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/market")]
public class MarketController(MarketService market) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
        => Ok(await market.GetOverviewAsync(ct));

    [HttpGet("coins/{symbol}")]
    public async Task<IActionResult> Coin(string symbol, CancellationToken ct)
    {
        var r = await market.GetCoinAsync(symbol, ct);
        return r.Success ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpGet("coins/{symbol}/history")]
    public async Task<IActionResult> History(string symbol, [FromQuery] string range = "90d", CancellationToken ct = default)
    {
        var r = await market.GetHistoryAsync(symbol, range, ct);
        return r.Success ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpGet("coins/{symbol}/indicators")]
    public async Task<IActionResult> Indicators(string symbol, CancellationToken ct)
    {
        var r = await market.GetIndicatorsAsync(symbol, ct);
        return r.Success ? Ok(r.Value) : NotFound(new { error = r.Error });
    }
}
