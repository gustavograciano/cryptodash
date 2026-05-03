using CryptoDash.Domain.Enums;

namespace CryptoDash.Application.DTOs;

public record HoldingDto(
    int CoinId,
    string Symbol,
    string Name,
    decimal Quantity,
    decimal AvgCostBrl,
    decimal PriceBrl,
    decimal ValueBrl,
    decimal Change24hPct,
    decimal PnlBrl,
    decimal PnlPct,
    DateTimeOffset? PriceUpdatedAt);

public record AllocationItemDto(
    int CoinId,
    string Symbol,
    decimal CurrentPct,
    decimal TargetPct,
    decimal DeviationPct,
    string DeviationSeverity);

public record PortfolioSummaryDto(
    decimal TotalBrl,
    decimal TotalCostBrl,
    decimal TotalPnlBrl,
    decimal TotalPnlPct,
    decimal Change24hBrl,
    decimal Change24hPct,
    IReadOnlyList<HoldingDto> Holdings,
    IReadOnlyList<AllocationItemDto> Allocation);

public record CreateTransactionRequest(
    int CoinId,
    TransactionType Type,
    decimal Quantity,
    decimal PriceBrl,
    decimal? FeeBrl,
    DateTimeOffset ExecutedAt,
    string? Notes);

public record TransactionDto(
    int Id,
    int CoinId,
    string Symbol,
    TransactionType Type,
    decimal Quantity,
    decimal PriceBrl,
    decimal TotalBrl,
    decimal FeeBrl,
    DateTimeOffset ExecutedAt,
    string? Notes);

public record AllocationTargetDto(int CoinId, string Symbol, decimal TargetPct);
public record UpdateAllocationRequest(IReadOnlyList<AllocationTargetDto> Targets);
