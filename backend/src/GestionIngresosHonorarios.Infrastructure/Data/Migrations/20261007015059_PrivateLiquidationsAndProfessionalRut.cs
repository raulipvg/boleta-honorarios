using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestionIngresosHonorarios.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrivateLiquidationsAndProfessionalRut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rut",
                table: "profesionales",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "atenciones_privadas",
                table: "periodos_mensuales",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "bruto_privado_total_clp",
                table: "periodos_mensuales",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "liquido_privado_total_clp",
                table: "periodos_mensuales",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "minutos_atencion_privados",
                table: "periodos_mensuales",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "retencion_privada_total_clp",
                table: "periodos_mensuales",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_periodos_mensuales_id_profesional_id_anio_mes",
                table: "periodos_mensuales",
                columns: new[] { "id", "profesional_id", "anio", "mes" });

            migrationBuilder.CreateTable(
                name: "instituciones_privadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instituciones_privadas", x => x.id);
                    table.CheckConstraint("ck_instituciones_privadas_nombre_no_vacio", "length(btrim(nombre)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "entidades_pagadoras_privadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    institucion_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rut = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entidades_pagadoras_privadas", x => x.id);
                    table.UniqueConstraint("AK_entidades_pagadoras_privadas_id_institucion_privada_id", x => new { x.id, x.institucion_privada_id });
                    table.CheckConstraint("ck_entidades_pagadoras_privadas_nombre_no_vacio", "length(btrim(razon_social)) > 0");
                    table.CheckConstraint("ck_entidades_pagadoras_privadas_rut_no_vacio", "length(btrim(rut)) > 0");
                    table.ForeignKey(
                        name: "FK_entidades_pagadoras_privadas_instituciones_privadas_institu~",
                        column: x => x.institucion_privada_id,
                        principalTable: "instituciones_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reglas_pago_privadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    institucion_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reglas_pago_privadas", x => x.id);
                    table.UniqueConstraint("AK_reglas_pago_privadas_id_institucion_privada_id", x => new { x.id, x.institucion_privada_id });
                    table.CheckConstraint("ck_reglas_pago_privadas_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_reglas_pago_privadas_instituciones_privadas_institucion_pri~",
                        column: x => x.institucion_privada_id,
                        principalTable: "instituciones_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "liquidaciones_privadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    periodo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profesional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anio = table.Column<short>(type: "smallint", nullable: false),
                    mes = table.Column<short>(type: "smallint", nullable: false),
                    quincena = table.Column<short>(type: "smallint", nullable: false),
                    institucion_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidad_pagadora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regla_pago_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rut_cobrador = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    numero_liquidacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_liquidacion = table.Column<DateOnly>(type: "date", nullable: false),
                    servicio_pago = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ejecutor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    total_servicio_clp = table.Column<long>(type: "bigint", nullable: true),
                    bruto_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    retencion_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    liquido_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    atenciones = table.Column<long>(type: "bigint", nullable: false),
                    minutos_por_atencion = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    minutos_totales = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tamano_archivo_bytes = table.Column<long>(type: "bigint", nullable: false),
                    importada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liquidaciones_privadas", x => x.id);
                    table.CheckConstraint("ck_liquidaciones_privadas_anio", "anio BETWEEN 1900 AND 32767");
                    table.CheckConstraint("ck_liquidaciones_privadas_archivo", "tamano_archivo_bytes BETWEEN 1 AND 1048576");
                    table.CheckConstraint("ck_liquidaciones_privadas_atenciones", "atenciones > 0");
                    table.CheckConstraint("ck_liquidaciones_privadas_importes_no_negativos", "bruto_total_clp >= 0 AND retencion_total_clp >= 0 AND liquido_total_clp >= 0 AND (total_servicio_clp IS NULL OR total_servicio_clp >= 0)");
                    table.CheckConstraint("ck_liquidaciones_privadas_json_exact", "bruto_total_clp <= 9007199254740991 AND retencion_total_clp <= 9007199254740991 AND liquido_total_clp <= 9007199254740991 AND atenciones <= 9007199254740991");
                    table.CheckConstraint("ck_liquidaciones_privadas_liquido_consistente", "retencion_total_clp <= bruto_total_clp AND liquido_total_clp = bruto_total_clp - retencion_total_clp");
                    table.CheckConstraint("ck_liquidaciones_privadas_mes", "mes BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_liquidaciones_privadas_minutos", "minutos_por_atencion > 0 AND minutos_totales >= 0");
                    table.CheckConstraint("ck_liquidaciones_privadas_quincena", "quincena IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_liquidaciones_privadas_entidades_pagadoras_privadas_entidad~",
                        columns: x => new { x.entidad_pagadora_id, x.institucion_privada_id },
                        principalTable: "entidades_pagadoras_privadas",
                        principalColumns: new[] { "id", "institucion_privada_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liquidaciones_privadas_periodos_mensuales_periodo_id_profes~",
                        columns: x => new { x.periodo_id, x.profesional_id, x.anio, x.mes },
                        principalTable: "periodos_mensuales",
                        principalColumns: new[] { "id", "profesional_id", "anio", "mes" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liquidaciones_privadas_profesionales_profesional_id",
                        column: x => x.profesional_id,
                        principalTable: "profesionales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liquidaciones_privadas_reglas_pago_privadas_regla_pago_id_i~",
                        columns: x => new { x.regla_pago_id, x.institucion_privada_id },
                        principalTable: "reglas_pago_privadas",
                        principalColumns: new[] { "id", "institucion_privada_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "instituciones_privadas",
                columns: new[] { "id", "activa", "created_at", "nombre", "updated_at" },
                values: new object[] { new Guid("8d1040c1-3da3-42fd-842a-c87eba660001"), true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sanatorio Alemán", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "entidades_pagadoras_privadas",
                columns: new[] { "id", "activa", "created_at", "razon_social", "institucion_privada_id", "rut" },
                values: new object[,]
                {
                    { new Guid("8d1040c1-3da3-42fd-842a-c87eba660002"), true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SERVICIOS SANATORIO ALEMAN SPA", new Guid("8d1040c1-3da3-42fd-842a-c87eba660001"), "76389986-1" },
                    { new Guid("8d1040c1-3da3-42fd-842a-c87eba660003"), true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "CLINICA SANATORIO ALEMAN SPA", new Guid("8d1040c1-3da3-42fd-842a-c87eba660001"), "88611600-4" }
                });

            migrationBuilder.InsertData(
                table: "reglas_pago_privadas",
                columns: new[] { "id", "activa", "codigo", "created_at", "institucion_privada_id", "version" },
                values: new object[] { new Guid("8d1040c1-3da3-42fd-842a-c87eba660004"), true, "SANATORIO_ALEMAN_PARTICIPACIONES", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("8d1040c1-3da3-42fd-842a-c87eba660001"), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_profesionales_rut",
                table: "profesionales",
                column: "rut",
                unique: true,
                filter: "rut IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_periodos_mensuales_privados_json_exact",
                table: "periodos_mensuales",
                sql: "bruto_privado_total_clp <= 9007199254740991 AND retencion_privada_total_clp <= 9007199254740991 AND liquido_privado_total_clp <= 9007199254740991 AND atenciones_privadas <= 9007199254740991");

            migrationBuilder.AddCheckConstraint(
                name: "ck_periodos_mensuales_privados_liquido",
                table: "periodos_mensuales",
                sql: "retencion_privada_total_clp <= bruto_privado_total_clp AND liquido_privado_total_clp = bruto_privado_total_clp - retencion_privada_total_clp");

            migrationBuilder.AddCheckConstraint(
                name: "ck_periodos_mensuales_privados_no_negativos",
                table: "periodos_mensuales",
                sql: "bruto_privado_total_clp >= 0 AND retencion_privada_total_clp >= 0 AND liquido_privado_total_clp >= 0 AND atenciones_privadas >= 0 AND minutos_atencion_privados >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_entidades_pagadoras_privadas_institucion_privada_id_activa",
                table: "entidades_pagadoras_privadas",
                columns: new[] { "institucion_privada_id", "activa" });

            migrationBuilder.CreateIndex(
                name: "IX_entidades_pagadoras_privadas_rut",
                table: "entidades_pagadoras_privadas",
                column: "rut",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_instituciones_privadas_nombre",
                table: "instituciones_privadas",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_entidad_pagadora_id_institucion_priv~",
                table: "liquidaciones_privadas",
                columns: new[] { "entidad_pagadora_id", "institucion_privada_id" });

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_periodo_id_profesional_id_anio_mes",
                table: "liquidaciones_privadas",
                columns: new[] { "periodo_id", "profesional_id", "anio", "mes" });

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_profesional_id_anio_mes",
                table: "liquidaciones_privadas",
                columns: new[] { "profesional_id", "anio", "mes" });

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_profesional_id_entidad_pagadora_id_n~",
                table: "liquidaciones_privadas",
                columns: new[] { "profesional_id", "entidad_pagadora_id", "numero_liquidacion", "anio", "mes", "quincena" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_profesional_id_sha256",
                table: "liquidaciones_privadas",
                columns: new[] { "profesional_id", "sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_regla_pago_id_institucion_privada_id",
                table: "liquidaciones_privadas",
                columns: new[] { "regla_pago_id", "institucion_privada_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reglas_pago_privadas_institucion_privada_id_codigo_version",
                table: "reglas_pago_privadas",
                columns: new[] { "institucion_privada_id", "codigo", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liquidaciones_privadas");

            migrationBuilder.DropTable(
                name: "entidades_pagadoras_privadas");

            migrationBuilder.DropTable(
                name: "reglas_pago_privadas");

            migrationBuilder.DropTable(
                name: "instituciones_privadas");

            migrationBuilder.DropIndex(
                name: "IX_profesionales_rut",
                table: "profesionales");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_periodos_mensuales_id_profesional_id_anio_mes",
                table: "periodos_mensuales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_periodos_mensuales_privados_json_exact",
                table: "periodos_mensuales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_periodos_mensuales_privados_liquido",
                table: "periodos_mensuales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_periodos_mensuales_privados_no_negativos",
                table: "periodos_mensuales");

            migrationBuilder.DropColumn(
                name: "rut",
                table: "profesionales");

            migrationBuilder.DropColumn(
                name: "atenciones_privadas",
                table: "periodos_mensuales");

            migrationBuilder.DropColumn(
                name: "bruto_privado_total_clp",
                table: "periodos_mensuales");

            migrationBuilder.DropColumn(
                name: "liquido_privado_total_clp",
                table: "periodos_mensuales");

            migrationBuilder.DropColumn(
                name: "minutos_atencion_privados",
                table: "periodos_mensuales");

            migrationBuilder.DropColumn(
                name: "retencion_privada_total_clp",
                table: "periodos_mensuales");
        }
    }
}
