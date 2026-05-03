using CryptoDash.Application.Abstractions;
using CryptoDash.Domain.Entities;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CryptoDash.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, IConfiguration config, CancellationToken ct = default)
    {
        if (db.Database.IsNpgsql())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        if (!await db.Coins.AnyAsync(ct))
        {
            db.Coins.AddRange(
                new Coin { Symbol = "BTC", Name = "Bitcoin", CoingeckoId = "bitcoin", RiskTier = RiskTier.Core, IsActive = true },
                new Coin { Symbol = "ETH", Name = "Ethereum", CoingeckoId = "ethereum", RiskTier = RiskTier.Core, IsActive = true },
                new Coin { Symbol = "SOL", Name = "Solana", CoingeckoId = "solana", RiskTier = RiskTier.Growth, IsActive = true },
                new Coin { Symbol = "AAVE", Name = "Aave", CoingeckoId = "aave", RiskTier = RiskTier.Defi, IsActive = true },
                new Coin { Symbol = "PAXG", Name = "PAX Gold", CoingeckoId = "pax-gold", RiskTier = RiskTier.Hedge, IsActive = true },
                new Coin { Symbol = "USDC", Name = "USD Coin", CoingeckoId = "usd-coin", RiskTier = RiskTier.Hedge, IsActive = true },
                new Coin { Symbol = "LINK", Name = "Chainlink", CoingeckoId = "chainlink", RiskTier = RiskTier.Growth, IsActive = true },
                new Coin { Symbol = "AVAX", Name = "Avalanche", CoingeckoId = "avalanche-2", RiskTier = RiskTier.Growth, IsActive = true },
                new Coin { Symbol = "LTC", Name = "Litecoin", CoingeckoId = "litecoin", RiskTier = RiskTier.Core, IsActive = true },
                new Coin { Symbol = "MATIC", Name = "Polygon", CoingeckoId = "matic-network", RiskTier = RiskTier.Growth, IsActive = true },
                new Coin { Symbol = "XRP", Name = "XRP", CoingeckoId = "ripple", RiskTier = RiskTier.Core, IsActive = true }
            );
            await db.SaveChangesAsync(ct);
        }

        if (!await db.TargetAllocations.AnyAsync(ct))
        {
            var coins = await db.Coins.ToListAsync(ct);
            var targets = new Dictionary<string, decimal> { ["BTC"] = 55, ["PAXG"] = 22, ["SOL"] = 15, ["AAVE"] = 8 };
            foreach (var (sym, pct) in targets)
            {
                var coin = coins.FirstOrDefault(c => c.Symbol == sym);
                if (coin is null) continue;
                db.TargetAllocations.Add(new TargetAllocation
                {
                    CoinId = coin.Id,
                    TargetPct = pct,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            await db.SaveChangesAsync(ct);
        }

        if (!await db.AportPlans.AnyAsync(ct))
        {
            db.AportPlans.Add(new AportPlan
            {
                AmountBrl = 300m,
                DayOfMonth = 5,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Users.AnyAsync(ct))
        {
            var email = config["Seed:UserEmail"] ?? "gustavo@cryptodash.local";
            var password = config["Seed:UserPassword"] ?? "ChangeMe@123";
            db.Users.Add(new User
            {
                Email = email,
                PasswordHash = hasher.Hash(password),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
