using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionIngresosHonorarios.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCebienEmailLiquidations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_archivo",
                table: "liquidaciones_privadas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_quincena",
                table: "liquidaciones_privadas");

            migrationBuilder.AlterColumn<long>(
                name: "tamano_archivo_bytes",
                table: "liquidaciones_privadas",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "storage_key",
                table: "liquidaciones_privadas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "rut_cobrador",
                table: "liquidaciones_privadas",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<short>(
                name: "quincena",
                table: "liquidaciones_privadas",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<string>(
                name: "numero_liquidacion",
                table: "liquidaciones_privadas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "nombre_archivo",
                table: "liquidaciones_privadas",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "fecha_liquidacion",
                table: "liquidaciones_privadas",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "ejecutor",
                table: "liquidaciones_privadas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "clave_negocio",
                table: "liquidaciones_privadas",
                type: "character(64)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cuerpo_fuente",
                table: "liquidaciones_privadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nombre_profesional_informado",
                table: "liquidaciones_privadas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "tipo_fuente",
                table: "liquidaciones_privadas",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.InsertData(
                table: "instituciones_privadas",
                columns: new[] { "id", "activa", "created_at", "nombre", "updated_at" },
                values: new object[] { new Guid("8d1040c1-3da3-42fd-842a-c87eba660005"), true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Centro Cebien", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                table: "entidades_pagadoras_privadas",
                columns: new[] { "id", "activa", "created_at", "razon_social", "institucion_privada_id", "rut" },
                values: new object[] { new Guid("8d1040c1-3da3-42fd-842a-c87eba660006"), true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "DR. BENJAMIN VICENTE PARADA Y CIA. LIMITADA", new Guid("8d1040c1-3da3-42fd-842a-c87eba660005"), "76015783-K" });

            migrationBuilder.InsertData(
                table: "reglas_pago_privadas",
                columns: new[] { "id", "activa", "codigo", "created_at", "institucion_privada_id", "version" },
                values: new object[] { new Guid("8d1040c1-3da3-42fd-842a-c87eba660007"), true, "CENTRO_CEBIEN_EMAIL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("8d1040c1-3da3-42fd-842a-c87eba660005"), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_privadas_profesional_id_clave_negocio",
                table: "liquidaciones_privadas",
                columns: new[] { "profesional_id", "clave_negocio" },
                unique: true,
                filter: "tipo_fuente = 2 AND clave_negocio IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_archivo",
                table: "liquidaciones_privadas",
                sql: "tamano_archivo_bytes IS NULL OR tamano_archivo_bytes BETWEEN 1 AND 1048576");

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_origen",
                table: "liquidaciones_privadas",
                sql: "(tipo_fuente = 1 AND rut_cobrador IS NOT NULL AND numero_liquidacion IS NOT NULL AND fecha_liquidacion IS NOT NULL AND quincena IS NOT NULL AND ejecutor IS NOT NULL AND storage_key IS NOT NULL AND nombre_archivo IS NOT NULL AND tamano_archivo_bytes IS NOT NULL AND nombre_profesional_informado IS NULL AND cuerpo_fuente IS NULL AND clave_negocio IS NULL) OR (tipo_fuente = 2 AND rut_cobrador IS NULL AND numero_liquidacion IS NULL AND fecha_liquidacion IS NULL AND quincena IS NULL AND ejecutor IS NULL AND total_servicio_clp IS NULL AND atenciones_reportadas_pdf IS NULL AND storage_key IS NULL AND nombre_archivo IS NULL AND tamano_archivo_bytes IS NULL AND length(btrim(nombre_profesional_informado)) > 0 AND length(btrim(cuerpo_fuente)) > 0 AND clave_negocio IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_quincena",
                table: "liquidaciones_privadas",
                sql: "quincena IS NULL OR quincena IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM liquidaciones_privadas WHERE tipo_fuente = 2) THEN
                        RAISE EXCEPTION 'No se puede revertir el soporte de Centro Cebien mientras existan liquidaciones de correo.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_liquidaciones_privadas_profesional_id_clave_negocio",
                table: "liquidaciones_privadas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_archivo",
                table: "liquidaciones_privadas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_origen",
                table: "liquidaciones_privadas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_quincena",
                table: "liquidaciones_privadas");

            migrationBuilder.DeleteData(
                table: "entidades_pagadoras_privadas",
                keyColumn: "id",
                keyValue: new Guid("8d1040c1-3da3-42fd-842a-c87eba660006"));

            migrationBuilder.DeleteData(
                table: "reglas_pago_privadas",
                keyColumn: "id",
                keyValue: new Guid("8d1040c1-3da3-42fd-842a-c87eba660007"));

            migrationBuilder.DeleteData(
                table: "instituciones_privadas",
                keyColumn: "id",
                keyValue: new Guid("8d1040c1-3da3-42fd-842a-c87eba660005"));

            migrationBuilder.DropColumn(
                name: "clave_negocio",
                table: "liquidaciones_privadas");

            migrationBuilder.DropColumn(
                name: "cuerpo_fuente",
                table: "liquidaciones_privadas");

            migrationBuilder.DropColumn(
                name: "nombre_profesional_informado",
                table: "liquidaciones_privadas");

            migrationBuilder.DropColumn(
                name: "tipo_fuente",
                table: "liquidaciones_privadas");

            migrationBuilder.AlterColumn<long>(
                name: "tamano_archivo_bytes",
                table: "liquidaciones_privadas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "storage_key",
                table: "liquidaciones_privadas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "rut_cobrador",
                table: "liquidaciones_privadas",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "quincena",
                table: "liquidaciones_privadas",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "numero_liquidacion",
                table: "liquidaciones_privadas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "nombre_archivo",
                table: "liquidaciones_privadas",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "fecha_liquidacion",
                table: "liquidaciones_privadas",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ejecutor",
                table: "liquidaciones_privadas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_archivo",
                table: "liquidaciones_privadas",
                sql: "tamano_archivo_bytes BETWEEN 1 AND 1048576");

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_quincena",
                table: "liquidaciones_privadas",
                sql: "quincena IN (1, 2)");
        }
    }
}
