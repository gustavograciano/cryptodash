namespace CryptoDash.Application.Abstractions;

/// <summary>
/// Fronteira transacional explícita, sem vazar tipos do EF Core para os serviços.
///
/// Existe porque escrita financeira precisa ser tudo-ou-nada: gravar metade de um aporte
/// e nenhum registro de que ele aconteceu é pior do que não gravar nada.
/// </summary>
public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
