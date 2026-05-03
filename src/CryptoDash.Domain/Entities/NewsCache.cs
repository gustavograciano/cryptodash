namespace CryptoDash.Domain.Entities;

public class NewsCache
{
    public long Id { get; set; }
    public string ExternalId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string Source { get; set; } = default!;
    public DateTimeOffset PublishedAt { get; set; }
    public List<string> CoinsMentioned { get; set; } = new();
    public string? Sentiment { get; set; }
    public string? RawPayload { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}
