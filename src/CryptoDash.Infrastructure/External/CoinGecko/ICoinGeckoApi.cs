using Refit;

namespace CryptoDash.Infrastructure.External.CoinGecko;

public interface ICoinGeckoApi
{
    [Get("/simple/price")]
    Task<Dictionary<string, CoinGeckoPriceResponse>> GetSimplePriceAsync(
        [AliasAs("ids")] string ids,
        [AliasAs("vs_currencies")] string vsCurrencies,
        [AliasAs("include_24hr_change")] bool include24hChange,
        [AliasAs("include_24hr_vol")] bool include24hVol,
        [AliasAs("include_market_cap")] bool includeMarketCap,
        CancellationToken ct = default);

    [Get("/coins/{id}/market_chart")]
    Task<CoinGeckoMarketChartResponse> GetMarketChartAsync(
        string id,
        [AliasAs("vs_currency")] string vsCurrency,
        [AliasAs("days")] int days,
        CancellationToken ct = default);

    [Get("/global")]
    Task<CoinGeckoGlobalResponse> GetGlobalAsync(CancellationToken ct = default);
}

public class CoinGeckoMarketChartResponse
{
    public List<List<decimal>> Prices { get; set; } = new();
    public List<List<decimal>> Total_volumes { get; set; } = new();
}

public class CoinGeckoGlobalResponse
{
    public CoinGeckoGlobalData? Data { get; set; }
}

public class CoinGeckoGlobalData
{
    public Dictionary<string, decimal>? Market_cap_percentage { get; set; }
    public Dictionary<string, decimal>? Total_market_cap { get; set; }
}

public class CoinGeckoPriceResponse
{
    public decimal? Brl { get; set; }
    public decimal? Usd { get; set; }
    public decimal? Brl_24h_change { get; set; }
    public decimal? Brl_24h_vol { get; set; }
    public decimal? Brl_market_cap { get; set; }
}
