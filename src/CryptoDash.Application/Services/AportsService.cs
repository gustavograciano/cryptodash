using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CryptoDash.Application.Abstractions;
using CryptoDash.Application.Common;
using CryptoDash.Application.DTOs;
using CryptoDash.Domain.Entities;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Services;

public class AportsService(IAppDbContext db)
{
    public async Task<AportPlanDto?> GetPlanAsync(CancellationToken ct = default)
    {
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(p => p.IsActive, ct);
        return plan is null ? null : new AportPlanDto(plan.Id, plan.AmountBrl, plan.DayOfMonth, plan.IsActive);
    }

    public async Task<AportPlanDto> UpsertPlanAsync(UpdateAportPlanRequest req, CancellationToken ct = default)
    {
        if (req.DayOfMonth < 1 || req.DayOfMonth > 28) throw new ArgumentException("dayOfMonth deve estar entre 1 e 28");
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        if (plan is null)
        {
            plan = new AportPlan { CreatedAt = DateTimeOffset.UtcNow };
            db.AportPlans.Add(plan);
        }
        plan.AmountBrl = req.AmountBrl;
        plan.DayOfMonth = req.DayOfMonth;
        plan.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return new AportPlanDto(plan.Id, plan.AmountBrl, plan.DayOfMonth, plan.IsActive);
    }

    public async Task<NextAportDto?> GetNextAsync(CancellationToken ct = default)
    {
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(p => p.IsActive, ct);
        if (plan is null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var next = new DateOnly(today.Year, today.Month, Math.Min(plan.DayOfMonth, DateTime.DaysInMonth(today.Year, today.Month)));
        if (next < today)
        {
            var m = today.AddMonths(1);
            next = new DateOnly(m.Year, m.Month, Math.Min(plan.DayOfMonth, DateTime.DaysInMonth(m.Year, m.Month)));
        }
        var daysUntil = next.DayNumber - today.DayNumber;

        var suggestions = await BuildSuggestionAsync(plan.AmountBrl, ct);
        return new NextAportDto(plan.Id, plan.AmountBrl, next, daysUntil, suggestions);
    }

    public async Task<IReadOnlyList<AportAllocationDto>> GetRebalanceSuggestionAsync(decimal? customAmount, CancellationToken ct = default)
    {
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(p => p.IsActive, ct);
        var amount = customAmount ?? plan?.AmountBrl ?? 300m;
        return await BuildSuggestionAsync(amount, ct);
    }

    private async Task<IReadOnlyList<AportAllocationDto>> BuildSuggestionAsync(decimal amount, CancellationToken ct)
    {
        var targets = await db.TargetAllocations
            .Include(t => t.Coin).ThenInclude(c => c.Holding)
            .Include(t => t.Coin).ThenInclude(c => c.PriceSnapshot)
            .Where(t => t.TargetPct > 0)
            .ToListAsync(ct);

        // Moeda sem cotação válida fica FORA da sugestão. Antes ela entrava com valor atual
        // zero — o que lhe dava o maior gap e, portanto, a maior fatia do aporte — e depois
        // era descartada em silêncio na execução. Hoje a execução recusa o aporte inteiro
        // nesse caso, então sugerir o que não se consegue precificar seria um beco sem saída.
        var inputs = targets
            .Where(t => t.Coin.PriceSnapshot is { PriceBrl: > 0m })
            .Select(t =>
            {
                var qty = t.Coin.Holding?.Quantity ?? 0m;
                var price = t.Coin.PriceSnapshot!.PriceBrl;
                return new DcaInput(t.CoinId, t.Coin.Symbol, qty * price, t.TargetPct);
            }).ToList();

        // Excluir uma moeda deixa os alvos restantes somando menos de 100% — e o ramo do
        // primeiro aporte do DcaAllocator distribui proporcional ao alvo, então o aporte
        // sairia incompleto (R$ 210 de R$ 300, com R$ 90 sumindo sem aviso). Renormalizar
        // redistribui a fatia da moeda ausente entre as que podem ser compradas.
        inputs = Renormalizar(inputs);

        var alloc = DcaAllocator.Allocate(inputs, amount);
        return alloc.Select(a => new AportAllocationDto(a.CoinId, a.Symbol, a.AmountBrl, a.CurrentValueBrl, a.TargetValueBrl, a.GapBrl)).ToList();
    }

    /// <summary>
    /// Reescala os alvos para somarem exatamente 100%, preservando as proporções relativas.
    /// A sobra de arredondamento vai para o maior alvo, para que a soma feche no centavo.
    /// </summary>
    private static List<DcaInput> Renormalizar(List<DcaInput> inputs)
    {
        var soma = inputs.Sum(i => i.TargetPct);
        if (inputs.Count == 0 || soma <= 0m || Math.Abs(soma - 100m) <= 0.001m) return inputs;

        var reescalado = inputs
            .Select(i => i with { TargetPct = Math.Round(i.TargetPct / soma * 100m, 2) })
            .ToList();

        var residuo = 100m - reescalado.Sum(i => i.TargetPct);
        if (residuo != 0m)
        {
            var maior = reescalado.IndexOf(reescalado.MaxBy(i => i.TargetPct)!);
            reescalado[maior] = reescalado[maior] with { TargetPct = reescalado[maior].TargetPct + residuo };
        }
        return reescalado;
    }

    /// <summary>Sinaliza reuso da mesma chave com payload diferente; o controller mapeia para 409.</summary>
    public const string ConflitoDeChave = "IDEMPOTENCY_KEY_CONFLICT";

    public async Task<Result<ExecuteAportResponse>> ExecuteAsync(ExecuteAportRequest req, CancellationToken ct = default)
    {
        // ── 1. Validação — inteira, ANTES de abrir transação ──────────────────────────
        // Dentro da fronteira transacional só devem restar falhas de banco.
        var plan = await db.AportPlans.FirstOrDefaultAsync(p => p.Id == req.PlanId, ct);
        if (plan is null) return Result<ExecuteAportResponse>.Fail("Plano não encontrado");
        if (!plan.IsActive) return Result<ExecuteAportResponse>.Fail("Plano de aporte está inativo");

        // Ordem estável por CoinId: dois aportes concorrentes que travem linhas de holdings
        // em ordens opostas produziriam deadlock no Postgres.
        var linhas = (req.Allocations ?? Array.Empty<AportAllocationDto>())
            .Where(a => a.AmountBrl != 0m)
            .OrderBy(a => a.CoinId)
            .ToList();

        if (linhas.Count == 0)
            return Result<ExecuteAportResponse>.Fail("Nenhuma alocação com valor a aportar");
        if (linhas.Any(a => a.AmountBrl < 0m))
            return Result<ExecuteAportResponse>.Fail("Alocação com valor negativo");
        if (linhas.Select(a => a.CoinId).Distinct().Count() != linhas.Count)
            return Result<ExecuteAportResponse>.Fail("Moeda repetida nas alocações");

        // O cliente não é fonte de verdade para dinheiro: a soma tem de fechar com o total.
        var somaAlocada = linhas.Sum(a => a.AmountBrl);
        if (Math.Abs(somaAlocada - req.AmountBrl) > 0.01m)
            return Result<ExecuteAportResponse>.Fail(
                $"Soma das alocações (R$ {somaAlocada:F2}) não confere com o total do aporte (R$ {req.AmountBrl:F2})");

        var snaps = await db.PriceSnapshots.ToDictionaryAsync(s => s.CoinId, ct);
        var coins = await db.Coins.Where(c => linhas.Select(l => l.CoinId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Symbol, ct);

        // Sem cotação válida não há como converter reais em quantidade. Antes o `continue`
        // pulava a linha em silêncio e mesmo assim gravava o valor integral no aporte —
        // o registro dizia R$ 300 e as transações somavam R$ 220. Agora recusa tudo.
        var semCotacao = linhas
            .Where(a => !snaps.TryGetValue(a.CoinId, out var s) || s.PriceBrl <= 0m)
            .Select(a => coins.GetValueOrDefault(a.CoinId, $"#{a.CoinId}"))
            .ToList();
        if (semCotacao.Count > 0)
            return Result<ExecuteAportResponse>.Fail(
                $"Sem cotação válida para: {string.Join(", ", semCotacao)}. Atualize os preços e refaça o aporte.");

        var novas = new List<Transaction>();
        foreach (var a in linhas)
        {
            var preco = snaps[a.CoinId].PriceBrl;
            var qty = Math.Round(a.AmountBrl / preco, 10);
            // Quantidade que arredonda para zero somaria custo sem lastro e inflaria o
            // custo médio da moeda para sempre.
            if (qty <= 0m)
                return Result<ExecuteAportResponse>.Fail(
                    $"Valor de R$ {a.AmountBrl:F2} em {coins.GetValueOrDefault(a.CoinId, $"#{a.CoinId}")} " +
                    "resulta em quantidade zero ao preço atual");

            novas.Add(new Transaction
            {
                CoinId = a.CoinId,
                Type = TransactionType.Buy,
                Quantity = qty,
                PriceBrl = preco,
                TotalBrl = a.AmountBrl,
                FeeBrl = 0m,
                ExecutedAt = req.ExecutedAt,
                Notes = $"Aporte automatico (plan #{plan.Id})",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var chave = ResolverChave(req, linhas);
        var fingerprint = Fingerprint(req, linhas);

        // ── 2. Atalho de repetição ────────────────────────────────────────────────────
        // Otimização, não garantia: em READ COMMITTED duas requisições simultâneas passam
        // as duas por aqui. Quem arbitra de verdade é o índice único, abaixo.
        var jaExiste = await db.AportsExecuted.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == chave, ct);
        if (jaExiste is not null) return Replay(jaExiste, fingerprint);

        // ── 3. Escrita — tudo-ou-nada ────────────────────────────────────────────────
        var executed = new AportExecuted
        {
            PlanId = plan.Id,
            AmountBrl = novas.Sum(t => t.TotalBrl),  // soma real, nunca o número do cliente
            ExecutedAt = req.ExecutedAt,
            TransactionIds = new List<int>(),
            IdempotencyKey = chave,
            RequestFingerprint = fingerprint,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var comitou = false;
        await using var trx = await db.BeginTransactionAsync(ct);
        try
        {
            // A chave é reivindicada no PRIMEIRO save, junto com as transações: o perdedor
            // de uma corrida bate no índice único antes de segurar lock em qualquer holding.
            db.Transactions.AddRange(novas);
            db.AportsExecuted.Add(executed);
            await db.SaveChangesAsync(ct);

            // Só agora os Ids existem. HoldingRecalculator consulta o banco, então precisa
            // que as transações já estejam persistidas — e o replay dele ordena por Id,
            // que enquanto Added seria temporário e negativo.
            executed.TransactionIds = novas.Select(t => t.Id).ToList();
            foreach (var coinId in novas.Select(t => t.CoinId).Distinct().OrderBy(id => id))
                await HoldingRecalculator.ApplyAsync(db, coinId, ct);

            await db.SaveChangesAsync(ct);
            await trx.CommitAsync(ct);
            comitou = true;
        }
        catch (DbUpdateException)
        {
            // Provider-agnóstico de propósito: em vez de inspecionar SQLSTATE (23505) ou
            // SqliteExtendedErrorCode (2067), perguntamos ao banco se a linha da nossa
            // chave passou a existir. Se passou, perdemos a corrida — devolvemos a resposta
            // do vencedor. Se não, a exceção era outra coisa e precisa subir.
            await trx.RollbackAsync(CancellationToken.None);
            db.ClearTracking();

            var vencedor = await db.AportsExecuted.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == chave, ct);
            if (vencedor is null) throw;
            return Replay(vencedor, fingerprint);
        }
        finally
        {
            // Qualquer caminho que não comitou — cancelamento, timeout, falha no commit —
            // deixaria entidades rastreadas que um SaveChanges posterior ressuscitaria.
            if (!comitou) db.ClearTracking();
        }

        return Result<ExecuteAportResponse>.Ok(
            new ExecuteAportResponse(executed.Id, executed.TransactionIds, executed.AmountBrl));
    }

    private static Result<ExecuteAportResponse> Replay(AportExecuted original, string fingerprint)
    {
        // Mesma chave com payload diferente não é repetição: é reuso indevido. Devolver a
        // resposta antiga faria um aporte real desaparecer sem erro e sem rastro.
        if (!string.Equals(original.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            return Result<ExecuteAportResponse>.Fail(
                $"{ConflitoDeChave}: esta chave de idempotência já foi usada com outro conteúdo " +
                $"(aporte #{original.Id}, R$ {original.AmountBrl:F2}).");

        return Result<ExecuteAportResponse>.Ok(
            new ExecuteAportResponse(original.Id, original.TransactionIds, original.AmountBrl, Replayed: true));
    }

    /// <summary>
    /// Chave do cliente quando houver; senão, chave natural derivada do servidor.
    ///
    /// A natural NÃO inclui as alocações de propósito: se os preços se moverem entre duas
    /// tentativas do mesmo aporte, o payload muda mas a intenção é a mesma — uma chave que
    /// dependesse dos valores deixaria a duplicata passar exatamente no caso mais provável.
    /// A granularidade de minuto vem de o modal congelar ExecutedAt no mount.
    /// </summary>
    private static string ResolverChave(ExecuteAportRequest req, IReadOnlyList<AportAllocationDto> linhas)
    {
        var doCliente = req.IdempotencyKey?.Trim();
        if (!string.IsNullOrWhiteSpace(doCliente))
            return "key:" + (doCliente.Length > 59 ? doCliente[..59] : doCliente);

        return $"nat:{req.PlanId}:{req.ExecutedAt.ToUniversalTime():yyyyMMddHHmm}";
    }

    private static string Fingerprint(ExecuteAportRequest req, IReadOnlyList<AportAllocationDto> linhas)
    {
        var canonico = new StringBuilder("aports.execute|v1")
            .Append("|plan=").Append(req.PlanId)
            .Append("|at=").Append(req.ExecutedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        foreach (var a in linhas.OrderBy(a => a.CoinId))
            canonico.Append('|').Append(a.CoinId).Append(':').Append(a.AmountBrl.ToString("F2", CultureInfo.InvariantCulture));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico.ToString()))).ToLowerInvariant();
    }
}
