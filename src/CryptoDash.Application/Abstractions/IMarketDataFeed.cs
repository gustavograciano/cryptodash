namespace CryptoDash.Application.Abstractions;

/// <param name="HasIntraday">
/// Falso quando a fonte devolveu um único ponto para o dia: nesse caso Open, High e Low são
/// repetição do fechamento, não extremos reais. A CoinGecko passa a granularidade diária
/// quando `days > 90`, então a MESMA coluna teria semânticas diferentes conforme a janela.
/// </param>
public record OhlcvPoint(
    DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume,
    bool HasIntraday = true);

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
