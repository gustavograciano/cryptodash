using Refit;

namespace CryptoDash.Infrastructure.External.AlternativeMe;

public interface IAlternativeMeApi
{
    [Get("/fng/")]
    Task<FngResponse> GetFearGreedAsync([AliasAs("limit")] int limit = 1, CancellationToken ct = default);
}

public class FngResponse
{
    public List<FngItem>? Data { get; set; }
}

public class FngItem
{
    public string? Value { get; set; }
    public string? Value_classification { get; set; }
    public string? Timestamp { get; set; }
}
