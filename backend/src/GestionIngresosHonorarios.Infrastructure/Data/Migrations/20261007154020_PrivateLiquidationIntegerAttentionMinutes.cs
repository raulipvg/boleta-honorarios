using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionIngresosHonorarios.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrivateLiquidationIntegerAttentionMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM liquidaciones_privadas
                        WHERE minutos_por_atencion <> trunc(minutos_por_atencion)
                           OR minutos_totales <> trunc(minutos_totales)
                           OR minutos_por_atencion < 1
                           OR minutos_por_atencion > 2147483647
                           OR minutos_totales < 0
                           OR minutos_totales > 9007199254740991
                    ) OR EXISTS (
                        SELECT 1
                        FROM periodos_mensuales
                        WHERE minutos_atencion_privados <> trunc(minutos_atencion_privados)
                           OR minutos_atencion_privados < 0
                           OR minutos_atencion_privados > 9007199254740991
                    ) THEN
                        RAISE EXCEPTION 'No se pueden migrar los minutos: existen valores fraccionarios o fuera del rango entero permitido.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_periodos_mensuales_privados_no_negativos",
                table: "periodos_mensuales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_minutos",
                table: "liquidaciones_privadas");

            migrationBuilder.AlterColumn<long>(
                name: "minutos_atencion_privados",
                table: "periodos_mensuales",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<long>(
                name: "minutos_totales",
                table: "liquidaciones_privadas",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<int>(
                name: "minutos_por_atencion",
                table: "liquidaciones_privadas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AddCheckConstraint(
                name: "ck_periodos_mensuales_privados_no_negativos",
                table: "periodos_mensuales",
                sql: "bruto_privado_total_clp >= 0 AND retencion_privada_total_clp >= 0 AND liquido_privado_total_clp >= 0 AND atenciones_privadas >= 0 AND minutos_atencion_privados BETWEEN 0 AND 9007199254740991");

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_minutos",
                table: "liquidaciones_privadas",
                sql: "minutos_por_atencion >= 1 AND minutos_totales BETWEEN 0 AND 9007199254740991");
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
                        WHERE minutos_totales > 999999999999
                    ) OR EXISTS (
                        SELECT 1
                        FROM periodos_mensuales
                        WHERE minutos_atencion_privados > 999999999999
                    ) THEN
                        RAISE EXCEPTION 'No se pueden revertir los minutos: existen valores que exceden numeric(18,6).';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_periodos_mensuales_privados_no_negativos",
                table: "periodos_mensuales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_minutos",
                table: "liquidaciones_privadas");

            migrationBuilder.AlterColumn<decimal>(
                name: "minutos_atencion_privados",
                table: "periodos_mensuales",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 0L);

            migrationBuilder.AlterColumn<decimal>(
                name: "minutos_totales",
                table: "liquidaciones_privadas",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "minutos_por_atencion",
                table: "liquidaciones_privadas",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddCheckConstraint(
                name: "ck_periodos_mensuales_privados_no_negativos",
                table: "periodos_mensuales",
                sql: "bruto_privado_total_clp >= 0 AND retencion_privada_total_clp >= 0 AND liquido_privado_total_clp >= 0 AND atenciones_privadas >= 0 AND minutos_atencion_privados >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_minutos",
                table: "liquidaciones_privadas",
                sql: "minutos_por_atencion > 0 AND minutos_totales >= 0");
        }
    }
}
