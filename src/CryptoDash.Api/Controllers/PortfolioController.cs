using CryptoDash.Application.DTOs;
using CryptoDash.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoDash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/portfolio")]
public class PortfolioController(PortfolioService portfolio, ReconciliationService reconciliation) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<PortfolioSummaryDto>> Summary(CancellationToken ct)
        => Ok(await portfolio.GetSummaryAsync(ct));

    [HttpGet("holdings")]
    public async Task<ActionResult<IReadOnlyList<HoldingDto>>> Holdings(CancellationToken ct)
        => Ok(await portfolio.GetHoldingsAsync(ct));

    [HttpGet("allocation")]
    public async Task<ActionResult<IReadOnlyList<AllocationTargetDto>>> Allocation(CancellationToken ct)
        => Ok(await portfolio.GetAllocationAsync(ct));

    [HttpPut("allocation")]
    public async Task<IActionResult> UpdateAllocation([FromBody] UpdateAllocationRequest req, CancellationToken ct)
    {
        var result = await portfolio.UpdateAllocationAsync(req, ct);
        return result.Success ? NoContent() : BadRequest(new { error = result.Error });
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> ListTransactions(
        [FromQuery] int? coinId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await portfolio.ListTransactionsAsync(coinId, skip, take, ct));

    [HttpPost("transactions")]
    public async Task<ActionResult<TransactionDto>> CreateTransaction([FromBody] CreateTransactionRequest req, CancellationToken ct)
    {
        var result = await portfolio.CreateTransactionAsync(req, ct);
        return result.Success
            ? CreatedAtAction(nameof(ListTransactions), new { coinId = req.CoinId }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Compara o razão com a posição. Vazio = as duas contam a mesma história.</summary>
    [HttpGet("reconciliation")]
    public async Task<IActionResult> Reconciliation(CancellationToken ct)
    {
        var r = await reconciliation.CheckAsync(ct);
        // 409 quando há divergência: o número exibido no dashboard não é confiável, e um
        // 200 faria monitoramento externo tratar isso como estado normal.
        return r.IsHealthy ? Ok(r) : Conflict(r);
    }

    /// <summary>
    /// Reconstrói posições a partir do razão. É o único reparo do sistema para divergência.
    /// </summary>
    [HttpPost("holdings/recalculate")]
    public async Task<IActionResult> Recalculate([FromQuery] int? coinId, CancellationToken ct)
        => Ok(await reconciliation.RecalculateAsync(coinId, ct));

    [HttpDelete("transactions/{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int id, CancellationToken ct)
    {
        var result = await portfolio.DeleteTransactionAsync(id, ct);
        return result.Success ? NoContent() : NotFound(new { error = result.Error });
    }
}
