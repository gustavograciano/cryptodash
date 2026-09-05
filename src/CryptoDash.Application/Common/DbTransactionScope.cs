using CryptoDash.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CryptoDash.Application.Common;

/// <summary>
/// Fronteira transacional para escrita financeira.
///
/// <para>
/// <c>IAppDbContext</c> não expõe <c>Database</c>, então a transação é obtida por cast. A
/// premissa é verdadeira por construção — <c>AppDbContext</c> é a única implementação e a DI
/// registra literalmente a mesma instância — mas é uma premissa de RUNTIME. Por isso a
/// checagem é explícita e falha alto: escrita financeira sem transação nunca pode acontecer
/// por degradação silenciosa.
/// </para>
/// <para>
/// Este helper existe porque o cast passou a aparecer em dois lugares
/// (<c>AportsService.ExecuteAsync</c> e <c>PortfolioService</c>). Era exatamente o critério
/// definido no desenho aprovado: promover à abstração depois da segunda ocorrência, não antes.
/// </para>
/// </summary>
public static class DbTransactionScope
{
    /// <summary>
    /// Executa <paramref name="trabalho"/> dentro de uma transação, commitando ao final.
    ///
    /// Em qualquer caminho que NÃO commite — exceção, cancelamento, falha no próprio commit —
    /// faz rollback e limpa o change tracker: o rollback desfaz no banco, mas as entidades
    /// continuam rastreadas e um SaveChanges posterior no mesmo contexto as ressuscitaria.
    ///
    /// Compõe com uma transação já aberta: neste caso apenas executa o trabalho e deixa a
    /// fronteira externa decidir sobre commit.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        IAppDbContext db,
        Func<CancellationToken, Task<T>> trabalho,
        CancellationToken ct = default)
    {
        var ctx = AsDbContext(db);

        if (ctx.Database.CurrentTransaction is not null)
            return await trabalho(ct);

        var comitou = false;
        await using var trx = await ctx.Database.BeginTransactionAsync(ct);
        try
        {
            var resultado = await trabalho(ct);
            await ctx.SaveChangesAsync(ct);
            await trx.CommitAsync(ct);
            comitou = true;
            return resultado;
        }
        finally
        {
            if (!comitou)
            {
                await trx.RollbackAsync(CancellationToken.None);
                ctx.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Abre a transação diretamente, para quem precisa tratar falhas do próprio commit.</summary>
    public static Task<IDbContextTransaction> BeginAsync(IAppDbContext db, CancellationToken ct = default)
        => AsDbContext(db).Database.BeginTransactionAsync(ct);

    public static void ClearTracking(IAppDbContext db) => AsDbContext(db).ChangeTracker.Clear();

    private static DbContext AsDbContext(IAppDbContext db) =>
        db as DbContext
        ?? throw new InvalidOperationException(
            "IAppDbContext precisa ser um DbContext para que a escrita financeira seja atômica.");
}
