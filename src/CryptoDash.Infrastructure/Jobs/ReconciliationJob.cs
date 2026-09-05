using CryptoDash.Application.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CryptoDash.Infrastructure.Jobs;

/// <summary>
/// Verifica de hora em hora se o razão e a posição contam a mesma história.
///
/// Divergência é registrada em <c>Error</c> nomeando moeda e magnitude. Sem sink persistente
/// de log isso ainda não é alerta ativo — é visibilidade, que é o que esta fase entrega.
/// </summary>
[DisallowConcurrentExecution]
public class ReconciliationJob(ReconciliationService reconciliation, ILogger<ReconciliationJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var relatorio = await reconciliation.CheckAsync(ct);

        if (relatorio.IsHealthy)
        {
            logger.LogInformation(
                "ReconciliationJob: {Count} moedas conferidas, razão e posição batem", relatorio.CoinsChecked);
            return;
        }

        foreach (var d in relatorio.Mismatches)
        {
            logger.LogError(
                "DIVERGENCIA razao/posicao | Moeda: {Symbol} | Razao: {Razao} | Posicao: {Posicao} | " +
                "Diferenca: {Diferenca} | Acao: POST /api/v1/portfolio/holdings/recalculate",
                d.Symbol, d.LedgerQuantity, d.HoldingQuantity, d.Difference);
        }

        logger.LogError(
            "ReconciliationJob: {Divergentes} de {Total} moedas divergentes — capital registrado nao confiavel",
            relatorio.Mismatches.Count, relatorio.CoinsChecked);
    }
}
