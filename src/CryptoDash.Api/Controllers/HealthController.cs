using CryptoDash.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public IActionResult Live() => Ok(new { status = "ok", at = DateTimeOffset.UtcNow });

    /// <summary>Idade máxima aceita para o preço mais recente antes de degradar o readiness.</summary>
    /// <remarks>
    /// O job roda a cada 15 min; 1 hora é folgado de propósito, para que uma falha isolada não
    /// vire alarme e uma parada real ainda apareça.
    /// </remarks>
    public static readonly TimeSpan IdadeMaximaDoPreco = TimeSpan.FromHours(1);

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        if (!dbOk) return StatusCode(503, new { status = "not-ready", db = false });

        // Banco de pé não significa sistema funcionando. Se o job de preço parou, o dashboard
        // segue exibindo números com aparência normal por tempo indefinido — e era exatamente
        // isso que ninguém percebia.
        // Ordenação no cliente: o SQLite do ambiente de desenvolvimento não suporta ORDER BY
        // sobre DateTimeOffset e a query traduzida lançava NotSupportedException — readiness
        // devolvendo 500 é pior do que não ter readiness. São ~dezenas de linhas.
        var carimbos = await db.PriceSnapshots.Select(p => p.UpdatedAt).ToListAsync(ct);
        var ultimo = carimbos.Count == 0 ? (DateTimeOffset?)null : carimbos.Max();

        var idade = ultimo is { } u ? DateTimeOffset.UtcNow - u : (TimeSpan?)null;
        var precoFresco = idade is { } i && i <= IdadeMaximaDoPreco;

        var corpo = new
        {
            status = precoFresco ? "ready" : "degraded",
            db = true,
            priceUpdatedAt = ultimo,
            priceAgeSeconds = idade?.TotalSeconds,
            maxPriceAgeSeconds = IdadeMaximaDoPreco.TotalSeconds
        };

        return precoFresco ? Ok(corpo) : StatusCode(503, corpo);
    }
}
