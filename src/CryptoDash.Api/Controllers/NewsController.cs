using CryptoDash.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoDash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/news")]
public class NewsController(NewsService news) : ControllerBase
{
    [HttpGet("digest/today")]
    public async Task<IActionResult> Today(CancellationToken ct)
    {
        var r = await news.GetDigestAsync(null, ct);
        return r.Success ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpGet("digest/{date}")]
    public async Task<IActionResult> ByDate(string date, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var d))
            return BadRequest(new { error = "Data inválida (use YYYY-MM-DD)" });
        var r = await news.GetDigestAsync(d, ct);
        return r.Success ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpGet("feed")]
    public async Task<IActionResult> Feed([FromQuery] string? coin, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await news.GetFeedAsync(coin, take, ct));
}
