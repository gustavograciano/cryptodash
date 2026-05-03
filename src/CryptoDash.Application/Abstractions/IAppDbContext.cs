using CryptoDash.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Coin> Coins { get; }
    DbSet<TargetAllocation> TargetAllocations { get; }
    DbSet<Holding> Holdings { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<PriceSnapshot> PriceSnapshots { get; }
    DbSet<PriceHistory> PriceHistory { get; }
    DbSet<MarketIndicator> MarketIndicators { get; }
    DbSet<AportPlan> AportPlans { get; }
    DbSet<AportExecuted> AportsExecuted { get; }
    DbSet<NewsCache> NewsCache { get; }
    DbSet<AiDigest> AiDigests { get; }
    DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
