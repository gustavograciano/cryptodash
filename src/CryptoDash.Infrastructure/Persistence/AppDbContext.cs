using System.Text.Json;
using CryptoDash.Application.Abstractions;
using CryptoDash.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CryptoDash.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Coin> Coins => Set<Coin>();
    public DbSet<TargetAllocation> TargetAllocations => Set<TargetAllocation>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();
    public DbSet<PriceHistory> PriceHistory => Set<PriceHistory>();
    public DbSet<MarketIndicator> MarketIndicators => Set<MarketIndicator>();
    public DbSet<AportPlan> AportPlans => Set<AportPlan>();
    public DbSet<AportExecuted> AportsExecuted => Set<AportExecuted>();
    public DbSet<NewsCache> NewsCache => Set<NewsCache>();
    public DbSet<AiDigest> AiDigests => Set<AiDigest>();
    public DbSet<User> Users => Set<User>();

    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => new EfAppTransaction(await Database.BeginTransactionAsync(ct));

    public void ClearTracking() => ChangeTracker.Clear();

    private sealed class EfAppTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction inner) : IAppTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        var isPostgres = Database.IsNpgsql();

        b.Entity<Coin>(e =>
        {
            e.HasIndex(c => c.Symbol).IsUnique();
            e.Property(c => c.Symbol).HasMaxLength(10).IsRequired();
            e.Property(c => c.Name).HasMaxLength(50).IsRequired();
            e.Property(c => c.CoingeckoId).HasMaxLength(50).IsRequired();
            e.Property(c => c.IconUrl).HasMaxLength(255);
            e.Property(c => c.RiskTier).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<TargetAllocation>(e =>
        {
            e.HasIndex(t => t.CoinId).IsUnique();
            e.Property(t => t.TargetPct).HasPrecision(5, 2);
            e.HasOne(t => t.Coin).WithOne(c => c.TargetAllocation)
                .HasForeignKey<TargetAllocation>(t => t.CoinId);
            if (isPostgres)
                e.ToTable(t => t.HasCheckConstraint("ck_target_pct_range", "target_pct >= 0 AND target_pct <= 100"));
        });

        b.Entity<Holding>(e =>
        {
            e.HasIndex(h => h.CoinId).IsUnique();
            e.Property(h => h.Quantity).HasPrecision(20, 10);
            e.Property(h => h.AvgCostBrl).HasPrecision(18, 2);
            e.HasOne(h => h.Coin).WithOne(c => c.Holding)
                .HasForeignKey<Holding>(h => h.CoinId);
        });

        b.Entity<Transaction>(e =>
        {
            e.HasIndex(t => new { t.CoinId, t.ExecutedAt });
            e.Property(t => t.Type).HasConversion<string>().HasMaxLength(10);
            e.Property(t => t.Quantity).HasPrecision(20, 10);
            e.Property(t => t.PriceBrl).HasPrecision(18, 2);
            e.Property(t => t.TotalBrl).HasPrecision(18, 2);
            e.Property(t => t.FeeBrl).HasPrecision(18, 2);
            e.HasOne(t => t.Coin).WithMany(c => c.Transactions).HasForeignKey(t => t.CoinId);
        });

        b.Entity<PriceSnapshot>(e =>
        {
            e.HasKey(p => p.CoinId);
            e.HasOne(p => p.Coin).WithOne(c => c.PriceSnapshot)
                .HasForeignKey<PriceSnapshot>(p => p.CoinId);
            e.Property(p => p.PriceBrl).HasPrecision(18, 2);
            e.Property(p => p.PriceUsd).HasPrecision(18, 2);
            e.Property(p => p.Change24hPct).HasPrecision(8, 2);
            e.Property(p => p.Change7dPct).HasPrecision(8, 2);
            e.Property(p => p.Change30dPct).HasPrecision(8, 2);
            e.Property(p => p.Volume24hBrl).HasPrecision(20, 2);
            e.Property(p => p.MarketCapBrl).HasPrecision(20, 2);
            e.Property(p => p.AthBrl).HasPrecision(18, 2);
            e.Property(p => p.AtlBrl).HasPrecision(18, 2);
        });

        b.Entity<PriceHistory>(e =>
        {
            e.HasIndex(p => new { p.CoinId, p.Date }).IsUnique();
            e.Property(p => p.OpenBrl).HasPrecision(18, 2);
            e.Property(p => p.HighBrl).HasPrecision(18, 2);
            e.Property(p => p.LowBrl).HasPrecision(18, 2);
            e.Property(p => p.CloseBrl).HasPrecision(18, 2);
            e.Property(p => p.VolumeBrl).HasPrecision(20, 2);
            e.HasOne(p => p.Coin).WithMany(c => c.PriceHistory).HasForeignKey(p => p.CoinId);
        });

        b.Entity<MarketIndicator>(e =>
        {
            e.HasIndex(m => m.Date).IsUnique();
            e.Property(m => m.FearGreedLabel).HasMaxLength(20);
            e.Property(m => m.BtcDominancePct).HasPrecision(5, 2);
            e.Property(m => m.TotalMarketCapUsd).HasPrecision(20, 2);
        });

        b.Entity<AportPlan>(e =>
        {
            e.Property(p => p.AmountBrl).HasPrecision(18, 2);
            if (isPostgres)
                e.ToTable(t => t.HasCheckConstraint("ck_day_of_month", "day_of_month >= 1 AND day_of_month <= 28"));
        });

        b.Entity<AportExecuted>(e =>
        {
            e.Property(p => p.AmountBrl).HasPrecision(18, 2);
            e.HasOne(p => p.Plan).WithMany().HasForeignKey(p => p.PlanId);

            // Fora do guard de provider de propósito: o índice único precisa existir também
            // no SQLite criado por EnsureCreated, para que os testes exercitem o MESMO
            // mecanismo de arbitragem de duplicatas que roda em produção.
            e.Property(p => p.IdempotencyKey).HasMaxLength(64).IsRequired();
            e.HasIndex(p => p.IdempotencyKey).IsUnique();
            e.Property(p => p.RequestFingerprint).HasMaxLength(64).IsRequired();
            if (isPostgres)
                e.Property(p => p.TransactionIds).HasColumnType("integer[]");
            else
                e.Property(p => p.TransactionIds).HasConversion(IntListConverter, IntListComparer);
        });

        b.Entity<NewsCache>(e =>
        {
            e.HasIndex(n => n.ExternalId).IsUnique();
            e.Property(n => n.ExternalId).HasMaxLength(50);
            e.Property(n => n.Title).IsRequired();
            e.Property(n => n.Url).HasMaxLength(500);
            e.Property(n => n.Source).HasMaxLength(100);
            e.Property(n => n.Sentiment).HasMaxLength(20);
            if (isPostgres)
            {
                e.Property(n => n.CoinsMentioned).HasColumnType("text[]");
                e.Property(n => n.RawPayload).HasColumnType("jsonb");
            }
            else
            {
                e.Property(n => n.CoinsMentioned).HasConversion(StringListConverter, StringListComparer);
            }
        });

        b.Entity<AiDigest>(e =>
        {
            e.HasIndex(d => d.Date).IsUnique();
            e.Property(d => d.Model).HasMaxLength(50);
            if (isPostgres)
            {
                e.Property(d => d.CoinsCovered).HasColumnType("text[]");
                e.Property(d => d.NewsIdsUsed).HasColumnType("bigint[]");
            }
            else
            {
                e.Property(d => d.CoinsCovered).HasConversion(StringListConverter, StringListComparer);
                e.Property(d => d.NewsIdsUsed).HasConversion(LongListConverter, LongListComparer);
            }
        });

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(255).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
        });

        base.OnModelCreating(b);
    }

    private static readonly ValueConverter<List<string>, string> StringListConverter =
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new());

    private static readonly ValueComparer<List<string>> StringListComparer =
        new((a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
            v => v.ToList());

    private static readonly ValueConverter<List<int>, string> IntListConverter =
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions?)null) ?? new());

    private static readonly ValueComparer<List<int>> IntListComparer =
        new((a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, HashCode.Combine),
            v => v.ToList());

    private static readonly ValueConverter<List<long>, string> LongListConverter =
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<long>>(v, (JsonSerializerOptions?)null) ?? new());

    private static readonly ValueComparer<List<long>> LongListComparer =
        new((a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (acc, x) => HashCode.Combine(acc, x)),
            v => v.ToList());
}
