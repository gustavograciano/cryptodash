using CryptoDash.Application.Abstractions;
using CryptoDash.Application.Common;
using CryptoDash.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Services;

public class AuthService(IAppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt)
{
    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            return Result<LoginResponse>.Fail("Credenciais inválidas");

        var token = jwt.Generate(user);
        return Result<LoginResponse>.Ok(new LoginResponse(token.Token, token.ExpiresAt));
    }
}
