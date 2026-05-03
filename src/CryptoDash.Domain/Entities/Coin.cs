using CryptoDash.Domain.Enums;

namespace CryptoDash.Domain.Entities;

public class Coin
{
    public int Id { get; set; }
    public string Symbol { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string CoingeckoId { get; set; } = default!;
    public string? IconUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public RiskTier RiskTier { get; set; }

    public TargetAllocation? TargetAllocation { get; set; }
    public Holding? Holding { get; set; }
    public PriceSnapshot? PriceSnapshot { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<PriceHistory> PriceHistory { get; set; } = new List<PriceHistory>();
}
