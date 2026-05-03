namespace CryptoDash.Application.Abstractions;

public record PriceQuote(
    string CoingeckoId,
    decimal PriceBrl,
    decimal PriceUsd,
    decimal Change24hPct,
    decimal Volume24hBrl,
    decimal MarketCapBrl);

public interface IPriceFeed
{
    Task<IReadOnlyList<PriceQuote>> GetSimplePricesAsync(IEnumerable<string> coingeckoIds, CancellationToken ct = default);
}
