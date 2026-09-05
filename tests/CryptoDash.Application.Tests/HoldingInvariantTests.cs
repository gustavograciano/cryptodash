using CryptoDash.Application.DTOs;
using CryptoDash.Application.Services;
using CryptoDash.Domain.Entities;
using CryptoDash.Domain.Enums;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Invariantes 1, 3, 4, 5 e 6 — o vínculo entre o razão (transactions) e a posição (holdings).
///
/// Estas são as propriedades que precisam continuar verdadeiras depois de QUALQUER refatoração
/// do cálculo de custo médio. Elas existem para ser a rede de proteção das correções da Fase 0.
/// </summary>
public class HoldingInvariantTests
{
    private static CreateTransactionRequest Buy(int coinId, decimal qty, decimal price, DateTimeOffset at, decimal fee = 0m)
        => new(coinId, TransactionType.Buy, qty, price, fee, at, null);

    private static CreateTransactionRequest Sell(int coinId, decimal qty, decimal price, DateTimeOffset at, decimal fee = 0m)
        => new(coinId, TransactionType.Sell, qty, price, fee, at, null);

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddDays(3);
    private static readonly DateTimeOffset T2 = T0.AddDays(6);

    // ─────────────────────────────────────────────────────────────────────────
    // INVARIANTE 1 — Σ(compras) − Σ(vendas) == Holding.Quantity
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(11)]
    [InlineData(2026)]
    [InlineData(777)]
    public async Task Invariante1_RazaoEPosicaoNuncaDivergem(int seed)
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);
        var rng = new Random(seed);

        decimal posicao = 0m;
        for (var i = 0; i < 40; i++)
        {
            // Só geramos vendas dentro da posição disponível: vender a descoberto é
            // objeto da Invariante 5, não desta. Aqui isolamos o vínculo razão↔posição.
            var comprar = posicao <= 0m || rng.Next(100) < 65;
            var qty = Math.Round((decimal)(rng.NextDouble() * 0.5 + 0.0001), 8);
            if (!comprar) qty = Math.Min(qty, posicao);
            if (qty <= 0m) continue;

            var preco = Math.Round((decimal)(rng.NextDouble() * 400_000 + 1_000), 2);
            var quando = T0.AddMinutes(i);

            var req = comprar ? Buy(coin.Id, qty, preco, quando) : Sell(coin.Id, qty, preco, quando);
            var r = await svc.CreateTransactionAsync(req);

            Assert.True(r.Success, $"transação {i} rejeitada: {r.Error}");
            posicao += comprar ? qty : -qty;
        }

        var holding = t.HoldingFor(coin.Id);
        Assert.NotNull(holding);
        Assert.Equal(t.LedgerNetQuantity(coin.Id), holding!.Quantity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INVARIANTE 3 — o custo médio é determinístico: o mesmo conjunto de transações
    // produz a mesma posição, independentemente da ordem em que foram inseridas.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invariante3_MesmoConjuntoDeTransacoes_ProduzMesmaPosicao_IndependenteDaOrdemDeInsercao()
    {
        // Compra e venda com ExecutedAt IDÊNTICO, ambas com lastro em qualquer ordem de
        // digitação. Sem critério de desempate que dependa apenas dos DADOS, a ordem de
        // inserção decide o custo médio final.
        var instante = T1;

        (decimal Qty, decimal AvgCost) PosicaoApos(bool compraPrimeiro)
        {
            using var t = new TestDb();
            var coin = t.AddCoin("BTC");
            var svc = new PortfolioService(t.Db);

            // Posição inicial, para que a venda no instante empatado tenha lastro
            // independentemente da ordem em que o par empatado for digitado.
            var inicial = svc.CreateTransactionAsync(Buy(coin.Id, 2m, 100_000m, T0)).GetAwaiter().GetResult();
            Assert.True(inicial.Success, inicial.Error);

            var compra = Buy(coin.Id, 1m, 120_000m, instante);
            var venda = Sell(coin.Id, 1m, 130_000m, instante);

            var primeira = svc.CreateTransactionAsync(compraPrimeiro ? compra : venda).GetAwaiter().GetResult();
            var segunda = svc.CreateTransactionAsync(compraPrimeiro ? venda : compra).GetAwaiter().GetResult();
            Assert.True(primeira.Success, primeira.Error);
            Assert.True(segunda.Success, segunda.Error);

            var h = t.HoldingFor(coin.Id)!;
            return (h.Quantity, h.AvgCostBrl);
        }

        var compraPrimeiro = PosicaoApos(true);
        var vendaPrimeiro = PosicaoApos(false);

        Assert.Equal(compraPrimeiro.Qty, vendaPrimeiro.Qty);
        Assert.Equal(compraPrimeiro.AvgCost, vendaPrimeiro.AvgCost);
        await Task.CompletedTask;
    }

    [Fact]
    public void Invariante3b_ReplayEhDeterministico_IndependenteDaOrdemDasLinhasVindasDoBanco()
    {
        // Sem ORDER BY, o banco não garante a ordem das linhas — e o plano de execução pode
        // mudar quando a tabela cresce ou um índice é criado. O replay tem de produzir o
        // mesmo número para qualquer permutação da mesma entrada.
        var txs = new List<Transaction>
        {
            new() { Id = 1, CoinId = 1, Type = TransactionType.Buy,  Quantity = 2m,   TotalBrl = 200_000m, FeeBrl = 40m, ExecutedAt = T0 },
            new() { Id = 2, CoinId = 1, Type = TransactionType.Buy,  Quantity = 1m,   TotalBrl = 120_000m, FeeBrl = 25m, ExecutedAt = T1 },
            new() { Id = 3, CoinId = 1, Type = TransactionType.Sell, Quantity = 1m,   TotalBrl = 130_000m, FeeBrl = 30m, ExecutedAt = T1 },
            new() { Id = 4, CoinId = 1, Type = TransactionType.Buy,  Quantity = 0.5m, TotalBrl = 70_000m,  FeeBrl = 10m, ExecutedAt = T1 },
            new() { Id = 5, CoinId = 1, Type = TransactionType.Sell, Quantity = 0.5m, TotalBrl = 72_000m,  FeeBrl = 12m, ExecutedAt = T2 },
        };

        var referencia = HoldingRecalculator.Replay(txs);
        var rng = new Random(7);

        for (var i = 0; i < 200; i++)
        {
            var embaralhado = txs.OrderBy(_ => rng.Next()).ToList();
            var resultado = HoldingRecalculator.Replay(embaralhado);

            Assert.Equal(referencia.Quantity, resultado.Quantity);
            Assert.Equal(referencia.AvgCostBrl, resultado.AvgCostBrl);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INVARIANTE 4 — apagar uma transação produz exatamente a posição que existiria
    // se ela nunca tivesse sido lançada.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invariante4_AposDeletar_PosicaoIgualAPortfolioSemAquelaTransacao()
    {
        // Referência: portfólio construído apenas com as transações que vão sobreviver.
        using var referencia = new TestDb();
        var coinRef = referencia.AddCoin("BTC");
        var svcRef = new PortfolioService(referencia.Db);
        await svcRef.CreateTransactionAsync(Buy(coinRef.Id, 0.5m, 200_000m, T0, fee: 30m));
        await svcRef.CreateTransactionAsync(Buy(coinRef.Id, 0.2m, 260_000m, T0.AddDays(2), fee: 12m));
        var esperado = referencia.HoldingFor(coinRef.Id)!;

        // Real: as mesmas duas, mais uma no meio que será apagada.
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(coin.Id, 0.5m, 200_000m, T0, fee: 30m));
        var descartavel = await svc.CreateTransactionAsync(Buy(coin.Id, 0.3m, 240_000m, T0.AddDays(1), fee: 20m));
        await svc.CreateTransactionAsync(Buy(coin.Id, 0.2m, 260_000m, T0.AddDays(2), fee: 12m));

        var del = await svc.DeleteTransactionAsync(descartavel.Value!.Id);
        Assert.True(del.Success, del.Error);

        var obtido = t.HoldingFor(coin.Id)!;
        Assert.Equal(esperado.Quantity, obtido.Quantity);
        Assert.Equal(esperado.AvgCostBrl, obtido.AvgCostBrl);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INVARIANTE 5 — vender mais do que se possui é rejeitado, nunca truncado.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invariante5_VendaMaiorQueAPosicao_EhRejeitada()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 100_000m, T0));

        var r = await svc.CreateTransactionAsync(Sell(coin.Id, 1.5m, 120_000m, T0.AddDays(1)));

        Assert.False(r.Success, "vender 1,5 BTC com posição de 1,0 deveria ser rejeitado");
    }

    [Fact]
    public async Task Invariante5b_VendaExcedente_NaoPodeDeixarRazaoEPosicaoDivergentes()
    {
        // Razão e posição precisam contar a mesma história mesmo depois de uma tentativa
        // de venda a descoberto. Antes divergiam — e a divergência sempre INFLAVA a posição.
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 100_000m, T0));
        var excedente = await svc.CreateTransactionAsync(Sell(coin.Id, 1.5m, 120_000m, T0.AddDays(1)));
        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 120_000m, T0.AddDays(2)));

        Assert.False(excedente.Success, "a venda excedente deveria ter sido rejeitada");

        var holding = t.HoldingFor(coin.Id)!;
        Assert.Equal(t.LedgerNetQuantity(coin.Id), holding.Quantity);
    }

    [Fact]
    public async Task Invariante5c_LancamentoRetroativo_QueTornaPosicaoFuturaNegativa_EhRejeitado()
    {
        // O saldo final não basta: uma venda retroativa pode ter lastro no instante dela e
        // ainda assim deixar negativa uma posição posterior. Por isso o guard simula o razão
        // inteiro, não apenas a posição corrente.
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 100_000m, T0));              // posição 1,0
        await svc.CreateTransactionAsync(Sell(coin.Id, 1m, 130_000m, T0.AddDays(5)));  // posição 0,0

        // Em T0+2 havia 1,0 BTC — a venda tem lastro naquele instante. Mas ela deixaria a
        // venda de T0+5 sem lastro, levando a posição a −0,5.
        var retroativa = await svc.CreateTransactionAsync(Sell(coin.Id, 0.5m, 120_000m, T0.AddDays(2)));

        Assert.False(retroativa.Success,
            "venda retroativa que torna negativa uma posição posterior deveria ser rejeitada");
        Assert.Equal(t.LedgerNetQuantity(coin.Id), t.HoldingFor(coin.Id)!.Quantity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INVARIANTE 6 — posição com quantidade > 0 nunca tem custo médio zero.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invariante6_CustoMedioNuncaEhZeroComQuantidadePositiva()
    {
        using var t = new TestDb();
        // Moeda barata. Hoje o universo é congelado no DbSeeder e nenhuma das 11 moedas
        // negocia abaixo de R$ 1 — mas o único caminho existente para cadastrar moeda é
        // inserir direto no banco, que é exatamente o que este teste faz.
        var coin = t.AddCoin("SHIB", RiskTier.Growth);
        var svc = new PortfolioService(t.Db);

        // R$ 70,00 em 1.000.000 de unidades a R$ 0,00007 cada.
        var r = await svc.CreateTransactionAsync(Buy(coin.Id, 1_000_000m, 0.00007m, T0));
        Assert.True(r.Success, r.Error);

        var holding = t.HoldingFor(coin.Id)!;
        Assert.True(holding.Quantity > 0m);
        Assert.True(
            holding.AvgCostBrl > 0m,
            $"custo médio zerou com {holding.Quantity} unidades em carteira — " +
            "a base de custo foi destruída e o P&L passa a reportar a posição inteira como lucro");
    }

    [Fact]
    public async Task Invariante6b_CustoTotalReconstruido_BateComODinheiroEfetivamenteGasto()
    {
        // O serviço descarta o totalCost real e persiste apenas Math.Round(totalCost/qty, 2);
        // o resumo depois RECONSTRÓI o custo como Quantity * AvgCostBrl. O erro por unidade
        // é multiplicado pela quantidade — logo escala com posições grandes.
        using var t = new TestDb();
        var coin = t.AddCoin("MATIC", RiskTier.Growth);
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(coin.Id, 250m, 1.207m, T0));   // R$ 301,75
        await svc.CreateTransactionAsync(Buy(coin.Id, 180m, 1.333m, T0.AddDays(1))); // R$ 239,94

        var gastoReal = 250m * 1.207m + 180m * 1.333m;
        var holding = t.HoldingFor(coin.Id)!;
        var custoReconstruido = holding.Quantity * holding.AvgCostBrl;

        Assert.True(
            Math.Abs(custoReconstruido - gastoReal) <= 0.01m,
            $"custo reconstruído R$ {custoReconstruido:F2} diverge do gasto real R$ {gastoReal:F2} " +
            $"em R$ {Math.Abs(custoReconstruido - gastoReal):F2}");
    }
}
