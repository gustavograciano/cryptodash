using CryptoDash.Domain.Entities;
using CryptoDash.Domain.Enums;
using CryptoDash.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Banco SQLite em memória, isolado por teste.
///
/// Usa o <see cref="AppDbContext"/> real (não um fake) para que os testes exerçam o mesmo
/// mapeamento EF, os mesmos conversores de valor e a mesma tradução de query que a aplicação.
/// A conexão fica aberta durante todo o ciclo de vida da fixture: fechar a conexão destrói
/// o banco :memory:.
///
/// Limitação conhecida e deliberada: SQLite é dinamicamente tipado e NÃO aplica
/// HasPrecision(18,2). Portanto estes testes verificam a aritmética dos serviços, não a
/// quantização feita pelo Postgres. Truncamento na fronteira do banco precisa de um teste
/// de integração contra Postgres real — está fora desta suíte e listado como próximo passo.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<AppDbContext> _contexts = new();

    public AppDbContext Db { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Db = NewContext();
        Db.Database.EnsureCreated();
    }

    /// <summary>
    /// Novo DbContext sobre o MESMO banco. Necessário para simular requisições
    /// concorrentes, que na aplicação recebem instâncias scoped distintas.
    /// </summary>
    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;
        var ctx = new AppDbContext(options);
        _contexts.Add(ctx);
        return ctx;
    }

    public Coin AddCoin(string symbol, RiskTier tier = RiskTier.Core)
    {
        var coin = new Coin
        {
            Symbol = symbol,
            Name = symbol,
            CoingeckoId = symbol.ToLowerInvariant(),
            IsActive = true,
            RiskTier = tier
        };
        Db.Coins.Add(coin);
        Db.SaveChanges();
        return coin;
    }

    public void SetPrice(int coinId, decimal priceBrl, decimal change24hPct = 0m)
    {
        var snap = Db.PriceSnapshots.FirstOrDefault(s => s.CoinId == coinId);
        if (snap is null)
        {
            Db.PriceSnapshots.Add(new PriceSnapshot
            {
                CoinId = coinId,
                PriceBrl = priceBrl,
                PriceUsd = 0m,
                Change24hPct = change24hPct,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            snap.PriceBrl = priceBrl;
            snap.Change24hPct = change24hPct;
            snap.UpdatedAt = DateTimeOffset.UtcNow;
        }
        Db.SaveChanges();
    }

    public void SetTarget(int coinId, decimal targetPct)
    {
        Db.TargetAllocations.Add(new TargetAllocation
        {
            CoinId = coinId,
            TargetPct = targetPct,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        Db.SaveChanges();
    }

    /// <summary>Soma líquida do razão: Σ compras − Σ vendas, direto das transações persistidas.</summary>
    public decimal LedgerNetQuantity(int coinId) =>
        Db.Transactions.Where(t => t.CoinId == coinId).AsEnumerable()
          .Sum(t => t.Type == TransactionType.Buy ? t.Quantity : -t.Quantity);

    public Holding? HoldingFor(int coinId) =>
        Db.Holdings.AsNoTracking().FirstOrDefault(h => h.CoinId == coinId);

    public void Dispose()
    {
        foreach (var ctx in _contexts) ctx.Dispose();
        _connection.Dispose();
    }
}
