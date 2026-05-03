namespace CryptoDash.Infrastructure.Auth;

public class JwtOptions
{
    public string Key { get; set; } = default!;
    public string Issuer { get; set; } = "cryptodash";
    public string Audience { get; set; } = "cryptodash";
    public int ExpiryMinutes { get; set; } = 10080;
}
