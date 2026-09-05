namespace CryptoDash.Domain.Entities;

public class Holding
{
    public int Id { get; set; }
    public int CoinId { get; set; }
    public Coin Coin { get; set; } = default!;
    public decimal Quantity { get; set; }
    /// <summary>
    /// Custo médio unitário. Existe para exibição — NÃO é mais a fonte da verdade do custo.
    /// </summary>
    public decimal AvgCostBrl { get; set; }

    /// <summary>
    /// Custo total da posição, persistido como FATO.
    ///
    /// Antes o custo total real era descartado e o resumo o reconstruía como
    /// <c>Quantity * AvgCostBrl</c> — e o erro de arredondamento por unidade era multiplicado
    /// pela quantidade. Uma posição de 430 MATIC reconstruía R$ 541,80 contra R$ 541,69
    /// efetivamente gastos.
    /// </summary>
    public decimal TotalCostBrl { get; set; }
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
