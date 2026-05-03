namespace CryptoDash.Domain.Entities;

public class PriceHistory
{
    public long Id { get; set; }
    public int CoinId { get; set; }
    public Coin Coin { get; set; } = default!;
    public DateOnly Date { get; set; }
    public decimal OpenBrl { get; set; }
    public decimal HighBrl { get; set; }
    public decimal LowBrl { get; set; }
    public decimal CloseBrl { get; set; }
    public decimal VolumeBrl { get; set; }
}
