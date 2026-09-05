namespace CryptoDash.Domain.Entities;

public class AportPlan
{
    public int Id { get; set; }
    public decimal AmountBrl { get; set; }
    public int DayOfMonth { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AportExecuted
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public AportPlan Plan { get; set; } = default!;

    /// <summary>Soma real das transações gravadas — nunca o valor que o cliente enviou.</summary>
    public decimal AmountBrl { get; set; }

    /// <summary>Instante declarado pelo usuário para a operação na corretora.</summary>
    public DateTimeOffset ExecutedAt { get; set; }

    public List<int> TransactionIds { get; set; } = new();

    /// <summary>
    /// Identidade da INTENÇÃO de aporte, sob índice único. É o banco, e não o código, que
    /// arbitra duplicatas: um SELECT antes do INSERT não sobrevive a duas requisições
    /// simultâneas.
    /// </summary>
    public string IdempotencyKey { get; set; } = default!;

    /// <summary>
    /// SHA-256 do payload canônico. Sem índice — dois aportes genuinamente idênticos, com
    /// chaves diferentes, são legítimos. Serve para distinguir repetição (mesma chave, mesmo
    /// payload → devolve o original) de reuso indevido (mesma chave, payload diferente →
    /// conflito). Sem ele, trocaríamos duplicação por perda silenciosa de evento financeiro.
    /// </summary>
    public string RequestFingerprint { get; set; } = default!;

    /// <summary>Quando a linha foi gravada — distinto de <see cref="ExecutedAt"/>, que é declarado.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
