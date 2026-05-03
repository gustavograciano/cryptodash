namespace CryptoDash.Domain.Entities;

public class TargetAllocation
{
    public int Id { get; set; }
    public int CoinId { get; set; }
    public Coin Coin { get; set; } = default!;
    public decimal TargetPct { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
