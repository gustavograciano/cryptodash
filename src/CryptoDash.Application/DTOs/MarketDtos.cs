namespace CryptoDash.Application.DTOs;

public record MarketOverviewDto(
    int FearGreedIndex,
    string FearGreedLabel,
    decimal BtcDominancePct,
    decimal TotalMarketCapUsd,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<MoverDto> TopGainers,
    IReadOnlyList<MoverDto> TopLosers);

public record MoverDto(string Symbol, string Name, decimal PriceBrl, decimal Change24hPct);

public record CoinSnapshotDto(
    int Id,
    string Symbol,
    string Name,
    decimal PriceBrl,
    decimal PriceUsd,
    decimal Change24hPct,
    decimal Volume24hBrl,
    decimal MarketCapBrl,
    DateTimeOffset? UpdatedAt);

public record OhlcvDto(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);

public record CoinIndicatorsDto(
    string Symbol,
    decimal? Ma200,
    string Ma200Signal,
    decimal? RsiWeekly14,
    string RsiSignal,
    decimal? Volatility30dAnnualized,
    decimal? DistanceFromMax90dPct,
    decimal? DistanceFromMax365dPct,
    decimal? DistanceFromMin90dPct);
