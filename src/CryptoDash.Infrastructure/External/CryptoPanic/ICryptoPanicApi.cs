using Refit;

namespace CryptoDash.Infrastructure.External.CryptoPanic;

public interface ICryptoPanicApi
{
    [Get("/posts/")]
    Task<CryptoPanicResponse> GetPostsAsync(
        [AliasAs("auth_token")] string authToken,
        [AliasAs("currencies")] string currencies,
        [AliasAs("public")] bool publicOnly = true,
        [AliasAs("kind")] string kind = "news",
        CancellationToken ct = default);
}

public class CryptoPanicResponse
{
    public List<CryptoPanicPost>? Results { get; set; }
}

public class CryptoPanicPost
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset? Published_at { get; set; }
    public CryptoPanicSource? Source { get; set; }
    public List<CryptoPanicCurrency>? Currencies { get; set; }
    public CryptoPanicVotes? Votes { get; set; }
}

public class CryptoPanicSource { public string? Title { get; set; } }
public class CryptoPanicCurrency { public string? Code { get; set; } }
public class CryptoPanicVotes
{
    public int Positive { get; set; }
    public int Negative { get; set; }
    public int Important { get; set; }
}
