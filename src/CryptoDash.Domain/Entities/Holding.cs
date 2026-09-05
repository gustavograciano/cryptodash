namespace CryptoDash.Domain.Entities;

public class Holding
{
    public int Id { get; set; }
    public int CoinId { get; set; }
    public Coin Coin { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal AvgCostBrl { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Token de concorrência. A posição é reconstruída por replay do razão inteiro
    /// (read-modify-write), então duas escritas concorrentes na mesma moeda sobrescreviam
    /// uma à outra em silêncio. Com o token, a segunda falha em vez de vencer com dado
    /// obsoleto — e o chamador reprocessa.
    ///
    /// Coluna gerenciada pela aplicação em vez do `xmin` do Postgres, para que o mecanismo
    /// seja idêntico no SQLite dos testes e em produção.
    /// </summary>
    public int Version { get; set; }
}
