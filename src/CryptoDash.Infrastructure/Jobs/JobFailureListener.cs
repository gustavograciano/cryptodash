using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Listener;

namespace CryptoDash.Infrastructure.Jobs;

/// <summary>
/// Registra falha de job em <c>Error</c>.
///
/// Antes, uma exceção em job virava silêncio: o dado do dia simplesmente não existia e nada
/// no sistema indicava por quê. Isto é visibilidade, não notificação ativa — alerta externo
/// depende do sink persistente de log, que é a Fase 1.
/// </summary>
public class JobFailureListener(ILogger<JobFailureListener> logger) : JobListenerSupport
{
    public override string Name => nameof(JobFailureListener);

    public override Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken ct = default)
    {
        if (jobException is null) return Task.CompletedTask;

        logger.LogError(
            jobException,
            "JOB FALHOU | Job: {Job} | Disparo: {Trigger} | Proxima tentativa so no proximo agendamento",
            context.JobDetail.Key.Name,
            context.Trigger.Key.Name);

        return Task.CompletedTask;
    }
}
