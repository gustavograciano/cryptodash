namespace CryptoDash.Domain.Entities;

public class AportPlan
{
    public int Id { get; set; }
    public decimal AmountBrl { get; set; }
    public int DayOfMonth { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AportExecuted
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public AportPlan Plan { get; set; } = default!;
    public decimal AmountBrl { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public List<int> TransactionIds { get; set; } = new();
}
