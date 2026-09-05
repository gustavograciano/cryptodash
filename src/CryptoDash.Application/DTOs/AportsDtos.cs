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

public record ExecuteAportRequest(
    int PlanId,
    decimal AmountBrl,
    DateTimeOffset ExecutedAt,
    IReadOnlyList<AportAllocationDto> Allocations,
    /// <summary>
    /// Identidade da intenção, vinda do header HTTP `Idempotency-Key`. Opcional: quando
    /// ausente o servidor deriva uma chave natural, o que protege bundles já publicados
    /// que ainda não enviam o header.
    /// </summary>
    string? IdempotencyKey = null);

/// <param name="AmountBrl">Soma real das transações gravadas.</param>
/// <param name="Replayed">
/// Verdadeiro quando esta requisição era repetição de uma já processada: nada novo foi
/// gravado e os ids devolvidos são os da execução original.
/// </param>
public record ExecuteAportResponse(
    int AportExecutedId,
    IReadOnlyList<int> TransactionIds,
    decimal AmountBrl = 0m,
    bool Replayed = false);
