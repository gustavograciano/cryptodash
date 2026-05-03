using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CryptoDash.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAportsAndNewsAndAiDigest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_digests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    summary_md = table.Column<string>(type: "text", nullable: false),
                    coins_covered = table.Column<List<string>>(type: "text[]", nullable: false),
                    news_ids_used = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_digests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "aport_plans",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    amount_brl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    day_of_month = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aport_plans", x => x.id);
                    table.CheckConstraint("ck_day_of_month", "day_of_month >= 1 AND day_of_month <= 28");
                });

            migrationBuilder.CreateTable(
                name: "news_cache",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    coins_mentioned = table.Column<List<string>>(type: "text[]", nullable: false),
                    sentiment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_cache", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "aports_executed",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plan_id = table.Column<int>(type: "integer", nullable: false),
                    amount_brl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    transaction_ids = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aports_executed", x => x.id);
                    table.ForeignKey(
                        name: "fk_aports_executed_aport_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "aport_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_digests_date",
                table: "ai_digests",
                column: "date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_aports_executed_plan_id",
                table: "aports_executed",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_cache_external_id",
                table: "news_cache",
                column: "external_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_digests");

            migrationBuilder.DropTable(
                name: "aports_executed");

            migrationBuilder.DropTable(
                name: "news_cache");

            migrationBuilder.DropTable(
                name: "aport_plans");
        }
    }
}
