using CryptoDash.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace CryptoDash.Infrastructure.External.AlternativeMe;

public class AlternativeMeFearGreedFeed(IAlternativeMeApi api, ILogger<AlternativeMeFearGreedFeed> logger) : IFearGreedFeed
{
    public async Task<FearGreedSnapshot?> GetCurrentAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await api.GetFearGreedAsync(1, ct);
            var item = r.Data?.FirstOrDefault();
            if (item is null) return null;

            int.TryParse(item.Value, out var value);
            DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            if (long.TryParse(item.Timestamp, out var ts))
                date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime);

            return new FearGreedSnapshot(value, item.Value_classification ?? "Neutral", date);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Alternative.me fng falhou");
            return null;
        }
    }
}
