using CryptoDash.Application.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CryptoDash.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class AIDigestJob(NewsService news, ILogger<AIDigestJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var d = await news.GenerateForTodayAsync(context.CancellationToken);
        if (d is null)
            logger.LogInformation("AIDigestJob: nenhuma notícia/LLM disponível");
        else
            logger.LogInformation("AIDigestJob: digest {Date} gerado ({Tokens} tokens)", d.Date, d.TokensUsed);
    }
}
