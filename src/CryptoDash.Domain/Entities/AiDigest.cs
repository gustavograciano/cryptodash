namespace CryptoDash.Domain.Entities;

public class AiDigest
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string SummaryMd { get; set; } = default!;
    public List<string> CoinsCovered { get; set; } = new();
    public List<long> NewsIdsUsed { get; set; } = new();
    public string Model { get; set; } = default!;
    public int TokensUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
