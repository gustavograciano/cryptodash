namespace CryptoDash.Application.Abstractions;

public record OhlcvPoint(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);

public record GlobalMarketSnapshot(decimal BtcDominancePct, decimal TotalMarketCapUsd);

public record FearGreedSnapshot(int Value, string Label, DateOnly Date);

public interface IMarketDataFeed
{
    Task<IReadOnlyList<OhlcvPoint>> GetMarketChartAsync(string coingeckoId, int days, CancellationToken ct = default);
    Task<GlobalMarketSnapshot?> GetGlobalAsync(CancellationToken ct = default);
}

public interface IFearGreedFeed
{
    Task<FearGreedSnapshot?> GetCurrentAsync(CancellationToken ct = default);
}
