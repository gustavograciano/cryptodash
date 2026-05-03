using CryptoDash.Domain.Enums;

namespace CryptoDash.Domain.Entities;

public class Transaction
{
    public int Id { get; set; }
    public int CoinId { get; set; }
    public Coin Coin { get; set; } = default!;
    public TransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal PriceBrl { get; set; }
    public decimal TotalBrl { get; set; }
    public decimal FeeBrl { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
