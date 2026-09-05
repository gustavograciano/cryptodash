using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoDash.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Custo total como fato persistido e precisão adequada a cripto.
    ///
    /// ATENÇÃO à mudança de precisão: numeric(18,2) tem 16 dígitos inteiros e 2 decimais;
    /// numeric(20,10) tem 10 inteiros e 10 decimais. A parte decimal cresce — que é o ponto,
    /// porque (18,2) zerava o custo de qualquer moeda sub-centavo — mas a parte inteira
    /// ENCOLHE de 16 para 10 dígitos. O teto por unidade passa a ser ~10 bilhões de reais,
    /// folgado para preço unitário de cripto e a razão pela qual esta narrowing é aceitável.
    /// </summary>
    public partial class AddHoldingTotalCostAndWiderPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "price_brl",
                table: "transactions",
                type: "numeric(20,10)",
                precision: 20,
                scale: 10,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "price_usd",
                table: "price_snapshots",
                type: "numeric(20,10)",
                precision: 20,
                scale: 10,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "price_brl",
                table: "price_snapshots",
                type: "numeric(20,10)",
                precision: 20,
                scale: 10,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "avg_cost_brl",
                table: "holdings",
                type: "numeric(20,10)",
                precision: 20,
                scale: 10,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "total_cost_brl",
                table: "holdings",
                type: "numeric(20,10)",
                precision: 20,
                scale: 10,
                nullable: false,
                defaultValue: 0m);

            // Sem backfill, toda posição existente ficaria com custo ZERO e o P&L passaria a
            // reportar a posição inteira como lucro. O melhor conhecido é a reconstrução que
            // o resumo fazia até aqui — ela carrega a deriva de arredondamento existente, mas
            // é o único dado disponível; aportes futuros já ficam corretos.
            //
            // Para eliminar a deriva nas posições antigas, rode depois do deploy:
            //   POST /api/v1/portfolio/holdings/recalculate
            // que reconstrói o custo a partir do razão.
            migrationBuilder.Sql(
                "UPDATE holdings SET total_cost_brl = quantity * avg_cost_brl;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "total_cost_brl",
                table: "holdings");

            migrationBuilder.AlterColumn<decimal>(
                name: "price_brl",
                table: "transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,10)",
                oldPrecision: 20,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "price_usd",
                table: "price_snapshots",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,10)",
                oldPrecision: 20,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "price_brl",
                table: "price_snapshots",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,10)",
                oldPrecision: 20,
                oldScale: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "avg_cost_brl",
                table: "holdings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,10)",
                oldPrecision: 20,
                oldScale: 10);
        }
    }
}
