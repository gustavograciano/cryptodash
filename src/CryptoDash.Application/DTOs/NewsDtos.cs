namespace CryptoDash.Application.DTOs;

public record NewsItemDto(
    long Id,
    string Title,
    string Url,
    string Source,
    DateTimeOffset PublishedAt,
    IReadOnlyList<string> CoinsMentioned,
    string? Sentiment);

public record AiDigestDto(
    int Id,
    DateOnly Date,
    string SummaryMd,
    IReadOnlyList<string> CoinsCovered,
    string Model,
    DateTimeOffset CreatedAt);
