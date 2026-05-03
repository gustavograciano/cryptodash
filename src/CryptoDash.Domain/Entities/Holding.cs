namespace CryptoDash.Domain.Entities;

public class Holding
{
    public int Id { get; set; }
    public int CoinId { get; set; }
    public Coin Coin { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal AvgCostBrl { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
