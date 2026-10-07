using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionIngresosHonorarios.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountSanatorioAlemanLiquidationsByDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_liquidaciones_privadas_periodos_mensuales_periodo_id_profes~",
                table: "liquidaciones_privadas");

            migrationBuilder.DropIndex(
                name: "IX_liquidaciones_privadas_periodo_id_profesional_id_anio_mes",
                table: "liquidaciones_privadas");

            migrationBuilder.DropIndex(
                name: "IX_liquidaciones_privadas_profesional_id_anio_mes",
                table: "liquidaciones_privadas");

            migrationBuilder.AddColumn<short>(
                name: "anio_contable",
                table: "liquidaciones_privadas",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "mes_contable",
                table: "liquidaciones_privadas",
                type: "smallint",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE liquidaciones_privadas
                SET anio_contable = anio,
                    mes_contable = mes;
                """);

            migrationBuilder.AlterColumn<short>(
                name: "anio_contable",
                table: "liquidaciones_privadas",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "mes_contable",
                table: "liquidaciones_privadas",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_periodo_id_profesional_id_anio_conta~",
                table: "liquidaciones_privadas",
                columns: new[] { "periodo_id", "profesional_id", "anio_contable", "mes_contable" });

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_profesional_id_anio_contable_mes_con~",
                table: "liquidaciones_privadas",
                columns: new[] { "profesional_id", "anio_contable", "mes_contable" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_anio_contable",
                table: "liquidaciones_privadas",
                sql: "anio_contable BETWEEN 1900 AND 32767");

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_mes_contable",
                table: "liquidaciones_privadas",
                sql: "mes_contable BETWEEN 1 AND 12");

            migrationBuilder.AddForeignKey(
                name: "FK_liquidaciones_privadas_periodos_mensuales_periodo_id_profes~",
                table: "liquidaciones_privadas",
                columns: new[] { "periodo_id", "profesional_id", "anio_contable", "mes_contable" },
                principalTable: "periodos_mensuales",
                principalColumns: new[] { "id", "profesional_id", "anio", "mes" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM liquidaciones_privadas
                        WHERE anio_contable <> anio OR mes_contable <> mes
                    ) THEN
                        RAISE EXCEPTION 'No se puede revertir la contabilización por fecha mientras existan liquidaciones asignadas a un mes distinto del período de servicio.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_liquidaciones_privadas_periodos_mensuales_periodo_id_profes~",
                table: "liquidaciones_privadas");

            migrationBuilder.DropIndex(
                name: "IX_liquidaciones_privadas_periodo_id_profesional_id_anio_conta~",
                table: "liquidaciones_privadas");

            migrationBuilder.DropIndex(
                name: "IX_liquidaciones_privadas_profesional_id_anio_contable_mes_con~",
                table: "liquidaciones_privadas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_anio_contable",
                table: "liquidaciones_privadas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_mes_contable",
                table: "liquidaciones_privadas");

            migrationBuilder.DropColumn(
                name: "anio_contable",
                table: "liquidaciones_privadas");

            migrationBuilder.DropColumn(
                name: "mes_contable",
                table: "liquidaciones_privadas");

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_periodo_id_profesional_id_anio_mes",
                table: "liquidaciones_privadas",
                columns: new[] { "periodo_id", "profesional_id", "anio", "mes" });

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_profesional_id_anio_mes",
                table: "liquidaciones_privadas",
                columns: new[] { "profesional_id", "anio", "mes" });

            migrationBuilder.AddForeignKey(
                name: "FK_liquidaciones_privadas_periodos_mensuales_periodo_id_profes~",
                table: "liquidaciones_privadas",
                columns: new[] { "periodo_id", "profesional_id", "anio", "mes" },
                principalTable: "periodos_mensuales",
                principalColumns: new[] { "id", "profesional_id", "anio", "mes" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
