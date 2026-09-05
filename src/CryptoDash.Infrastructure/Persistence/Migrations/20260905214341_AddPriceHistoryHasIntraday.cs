using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoDash.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Marca velas que não têm intraday real.
    ///
    /// O default `false` para linhas existentes é deliberado e conservador: não sabemos quais
    /// foram gravadas com granularidade horária, e uma vela não pode passar a alegar extremos
    /// que não se pode provar. O `PriceHistoryJob` reescreve os últimos dias a cada execução,
    /// então o histórico recente se corrige sozinho.
    ///
    /// ATENÇÃO ao volume histórico: linhas gravadas antes desta correção têm volume somado a
    /// partir de leituras rolantes de 24h e estão infladas em até ~24x. Elas só são corrigidas
    /// quando o job as reescreve, o que alcança apenas a janela recente. Volume antigo
    /// permanece inflado — e ele não alimenta nenhum indicador hoje.
    /// </summary>
    public partial class AddPriceHistoryHasIntraday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_intraday",
                table: "price_history",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_intraday",
                table: "price_history");
        }
    }
}
