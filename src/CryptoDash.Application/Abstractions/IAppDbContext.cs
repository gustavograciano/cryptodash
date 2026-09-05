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

    /// <summary>Abre uma transação explícita. Ver <see cref="IAppTransaction"/>.</summary>
    Task<IAppTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Descarta tudo que o change tracker está rastreando. Obrigatório em qualquer caminho
    /// que NÃO commita: o rollback desfaz no banco, mas as entidades continuam rastreadas e
    /// um SaveChanges posterior no mesmo contexto as ressuscitaria.
    /// </summary>
    void ClearTracking();
}
