using CryptoDash.Domain.Entities;

namespace CryptoDash.Application.Abstractions;

public record JwtToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    JwtToken Generate(User user);
}
