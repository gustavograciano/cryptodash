namespace CryptoDash.Domain.Entities;

public class MarketIndicator
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int FearGreedIndex { get; set; }
    public string FearGreedLabel { get; set; } = default!;
    public decimal BtcDominancePct { get; set; }
    public decimal TotalMarketCapUsd { get; set; }
}
