using CryptoDash.Application.DTOs;
using CryptoDash.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoDash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/aports")]
public class AportsController(AportsService aports, PortfolioService portfolio) : ControllerBase
{
    [HttpGet("plan")]
    public async Task<IActionResult> GetPlan(CancellationToken ct)
    {
        var plan = await aports.GetPlanAsync(ct);
        return plan is null ? NotFound() : Ok(plan);
    }

    [HttpPut("plan")]
    public async Task<IActionResult> UpdatePlan([FromBody] UpdateAportPlanRequest req, CancellationToken ct)
    {
        try { return Ok(await aports.UpsertPlanAsync(req, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("next")]
    public async Task<IActionResult> Next(CancellationToken ct)
    {
        var n = await aports.GetNextAsync(ct);
        return n is null ? NotFound(new { error = "Sem plano ativo" }) : Ok(n);
    }

    [HttpGet("/api/v1/portfolio/rebalance-suggestion")]
    public async Task<IActionResult> Rebalance([FromQuery] decimal? amountBrl, CancellationToken ct)
    {
        _ = portfolio;
        return Ok(await aports.GetRebalanceSuggestionAsync(amountBrl, ct));
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteAportRequest req, CancellationToken ct)
    {
        var r = await aports.ExecuteAsync(req, ct);
        return r.Success ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }
}
