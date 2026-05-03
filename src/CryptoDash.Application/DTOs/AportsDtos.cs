namespace CryptoDash.Application.DTOs;

public record AportPlanDto(int Id, decimal AmountBrl, int DayOfMonth, bool IsActive);

public record UpdateAportPlanRequest(decimal AmountBrl, int DayOfMonth, bool IsActive);

public record AportAllocationDto(int CoinId, string Symbol, decimal AmountBrl, decimal CurrentValueBrl, decimal TargetValueBrl, decimal GapBrl);

public record NextAportDto(
    int? PlanId,
    decimal AmountBrl,
    DateOnly NextDate,
    int DaysUntil,
    IReadOnlyList<AportAllocationDto> Suggested);

public record ExecuteAportRequest(int PlanId, decimal AmountBrl, DateTimeOffset ExecutedAt, IReadOnlyList<AportAllocationDto> Allocations);

public record ExecuteAportResponse(int AportExecutedId, IReadOnlyList<int> TransactionIds);
