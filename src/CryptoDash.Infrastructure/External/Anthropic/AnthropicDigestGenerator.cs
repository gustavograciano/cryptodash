using System.Text;
using CryptoDash.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CryptoDash.Infrastructure.External.Anthropic;

public class AnthropicDigestGenerator(IAnthropicApi api, IConfiguration config, ILogger<AnthropicDigestGenerator> logger) : ILlmDigestGenerator
{
    private const string SystemPrompt = """
        Voce e um analista de cripto. Resuma fatos de forma neutra, sem dar conselho de investimento.
        Foque no que afeta diretamente as moedas listadas. Use bullets curtos.
        Nao especule sobre preco futuro. Nao use linguagem promocional.
        """;

    public async Task<DigestGeneration> GenerateAsync(IReadOnlyList<string> coins, IReadOnlyList<NewsItemForPrompt> news, CancellationToken ct = default)
    {
        var apiKey = config["Anthropic:ApiKey"];
        var model = config["Anthropic:Model"] ?? "claude-sonnet-4-5";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Anthropic ApiKey ausente; gerando digest fallback");
            return new DigestGeneration(BuildFallback(news), "fallback", 0);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Coins do portfolio: {string.Join(", ", coins)}.");
        sb.AppendLine("Noticias das ultimas 24h:");
        foreach (var n in news)
            sb.AppendLine($"- [{n.Source}] {n.Title} ({string.Join(",", n.Coins)}) — {n.Url}");
        sb.AppendLine();
        sb.AppendLine("Gere um resumo em markdown com:");
        sb.AppendLine("## Destaques do dia");
        sb.AppendLine("## Por moeda (apenas as com novidade relevante)");
        sb.AppendLine("## Contexto macro (se aplicavel)");

        try
        {
            var resp = await api.CreateMessageAsync(apiKey, "2023-06-01",
                new AnthropicRequest(model, 1500, SystemPrompt, new[] { new AnthropicMessage("user", sb.ToString()) }),
                ct);
            var text = string.Concat((resp.Content ?? new()).Where(c => c.Type == "text").Select(c => c.Text));
            var tokens = (resp.Usage?.Input_tokens ?? 0) + (resp.Usage?.Output_tokens ?? 0);
            return new DigestGeneration(string.IsNullOrWhiteSpace(text) ? BuildFallback(news) : text, resp.Model ?? model, tokens);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Anthropic falhou; usando fallback");
            return new DigestGeneration(BuildFallback(news), "fallback", 0);
        }
    }

    private static string BuildFallback(IReadOnlyList<NewsItemForPrompt> news)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Destaques do dia");
        sb.AppendLine();
        sb.AppendLine("_Resumo automatico — IA indisponivel no momento._");
        sb.AppendLine();
        foreach (var n in news.Take(8))
            sb.AppendLine($"- **{n.Source}**: {n.Title}");
        return sb.ToString();
    }
}
