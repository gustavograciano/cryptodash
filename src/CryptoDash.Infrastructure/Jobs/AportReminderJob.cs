using CryptoDash.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CryptoDash.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class AportReminderJob(AppDbContext db, ILogger<AportReminderJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(p => p.IsActive, ct);
        if (plan is null) return;

        var today = DateTime.UtcNow.Date;
        if (today.Day != Math.Min(plan.DayOfMonth, DateTime.DaysInMonth(today.Year, today.Month)))
            return;

        logger.LogInformation("AportReminderJob: hoje é dia de aporte (R$ {Amount}). Verifique o dashboard.", plan.AmountBrl);
    }
}
