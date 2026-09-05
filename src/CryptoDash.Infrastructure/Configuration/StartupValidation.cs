namespace CryptoDash.Infrastructure.Configuration;

/// <summary>
/// Validação de configuração no boot. Falha alto e cedo, antes de o Kestrel aceitar
/// requisições e antes de o seeder criar conta alguma.
///
/// <para>
/// Existe porque os dois guards que havia eram inalcançáveis na prática: o
/// <c>?? throw</c> da chave JWT nunca disparava (o <c>appsettings.json</c> sempre fornecia
/// valor), e o <c>?? "ChangeMe@123"</c> da senha de seed não dispara para string vazia —
/// <c>""</c> não é <c>null</c> —, de modo que um deploy com variável faltando criava a única
/// conta do sistema com senha VAZIA.
/// </para>
/// </summary>
public static class StartupValidation
{
    public const int TamanhoMinimoChaveJwt = 32;
    public const int TamanhoMinimoSenha = 8;

    /// <summary>Chaves que já vazaram no repositório e não podem assinar token em lugar nenhum.</summary>
    private static readonly string[] ChavesProibidas =
    {
        "dev-only-key-change-me-please-min-32-characters-long",
        "change-me-min-32-chars-please-please-please",
        "generate-with-openssl-rand-hex-32",
    };

    private static readonly string[] SenhasProibidas = { "ChangeMe@123", "changeme", "password" };

    /// <param name="permitirValorDeExemplo">
    /// Verdadeiro apenas em Development. Comprimento e não-vazio são exigidos em todo
    /// ambiente; o que muda é a recusa de chaves publicadas, que fora de Development é
    /// inegociável e em Development apenas quebraria o loop local.
    /// </param>
    public static void ValidarChaveJwt(string? chave, bool permitirValorDeExemplo = false)
    {
        if (string.IsNullOrWhiteSpace(chave))
            throw new InvalidOperationException(
                "Jwt:Key ausente. Gere uma com `openssl rand -hex 32` e passe em JWT_KEY.");

        if (chave.Length < TamanhoMinimoChaveJwt)
            throw new InvalidOperationException(
                $"Jwt:Key tem {chave.Length} caracteres; o mínimo para HMAC-SHA256 é {TamanhoMinimoChaveJwt}.");

        if (!permitirValorDeExemplo && ChavesProibidas.Contains(chave, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Jwt:Key é um valor de exemplo publicado no repositório — qualquer pessoa poderia " +
                "forjar um token válido. Gere uma com `openssl rand -hex 32`.");
    }

    public static void ValidarSenhaDeSeed(string? senha, bool permitirValorDeExemplo = false)
    {
        if (string.IsNullOrWhiteSpace(senha))
            throw new InvalidOperationException(
                "Seed:UserPassword vazia. A única conta do sistema seria criada SEM senha. " +
                "Defina SEED_PASSWORD.");

        if (senha.Length < TamanhoMinimoSenha)
            throw new InvalidOperationException(
                $"Seed:UserPassword tem {senha.Length} caracteres; o mínimo é {TamanhoMinimoSenha}.");

        if (!permitirValorDeExemplo && SenhasProibidas.Contains(senha, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Seed:UserPassword é uma senha de exemplo publicada no repositório. Escolha outra.");
    }
}
