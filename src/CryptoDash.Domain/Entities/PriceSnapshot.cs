namespace CryptoDash.Domain.Entities;

public class PriceSnapshot
{
    public int CoinId { get; set; }
    public Coin Coin { get; set; } = default!;
    public decimal PriceBrl { get; set; }
    public decimal PriceUsd { get; set; }
    public decimal Change24hPct { get; set; }
    public decimal Change7dPct { get; set; }
    public decimal Change30dPct { get; set; }
    public decimal Volume24hBrl { get; set; }
    public decimal MarketCapBrl { get; set; }
    public decimal AthBrl { get; set; }
    public decimal AtlBrl { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
