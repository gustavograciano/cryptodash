using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoDash.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Idempotência de aportes: chave sob índice único, fingerprint do payload e carimbo de
    /// gravação.
    ///
    /// O scaffold do `dotnet ef` gerou AddColumn NOT NULL com defaultValue "" seguido de
    /// CreateIndex UNIQUE — o que FALHA em qualquer base com duas ou mais linhas em
    /// aports_executed, porque todas as linhas antigas ficariam com a mesma chave vazia.
    /// A sequência correta é: adicionar anulável, preencher com valor distinto por linha,
    /// só então tornar obrigatória e indexar.
    /// </summary>
    public partial class AddAportExecutedIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── created_at ───────────────────────────────────────────────────────────
            migrationBuilder.AddColumn<System.DateTimeOffset>(
                name: "created_at",
                table: "aports_executed",
                type: "timestamp with time zone",
                nullable: true);

            // Aportes antigos não têm registro de quando foram gravados; o melhor
            // conhecido é o instante declarado da execução.
            migrationBuilder.Sql(
                "UPDATE aports_executed SET created_at = executed_at WHERE created_at IS NULL;");

            migrationBuilder.AlterColumn<System.DateTimeOffset>(
                name: "created_at",
                table: "aports_executed",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(System.DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            // ── idempotency_key ──────────────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "aports_executed",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Um valor DISTINTO por linha existente. O prefixo 'legacy:' não colide com os
            // espaços de nome usados em runtime ('nat:' e 'key:'), então nenhum aporte novo
            // pode ser confundido com um pré-existente.
            migrationBuilder.Sql(
                "UPDATE aports_executed SET idempotency_key = 'legacy:' || id WHERE idempotency_key IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "aports_executed",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            // ── request_fingerprint ──────────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "request_fingerprint",
                table: "aports_executed",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Linhas antigas não têm payload registrado. O sentinela garante que uma
            // repetição jamais case por fingerprint com um aporte pré-existente.
            migrationBuilder.Sql(
                "UPDATE aports_executed SET request_fingerprint = 'legacy' WHERE request_fingerprint IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "request_fingerprint",
                table: "aports_executed",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            // Só agora, com todas as linhas preenchidas e distintas, o índice é seguro.
            migrationBuilder.CreateIndex(
                name: "ix_aports_executed_idempotency_key",
                table: "aports_executed",
                column: "idempotency_key",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_aports_executed_idempotency_key",
                table: "aports_executed");

            migrationBuilder.DropColumn(name: "created_at", table: "aports_executed");
            migrationBuilder.DropColumn(name: "idempotency_key", table: "aports_executed");
            migrationBuilder.DropColumn(name: "request_fingerprint", table: "aports_executed");
        }
    }
}
