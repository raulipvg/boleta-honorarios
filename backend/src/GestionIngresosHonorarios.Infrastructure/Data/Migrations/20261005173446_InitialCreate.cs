using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GestionIngresosHonorarios.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instituciones_publicas",
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
                    table.PrimaryKey("PK_instituciones_publicas", x => x.id);
                    table.CheckConstraint("ck_instituciones_publicas_nombre_not_blank", "length(btrim(nombre)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "retencion_boleta_honorarios",
                columns: table => new
                {
                    anio = table.Column<short>(type: "smallint", nullable: false),
                    porcentaje = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retencion_boleta_honorarios", x => x.anio);
                    table.CheckConstraint("ck_retencion_boleta_anio", "anio BETWEEN 1900 AND 32767");
                    table.CheckConstraint("ck_retencion_boleta_porcentaje", "porcentaje BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.CheckConstraint("ck_roles_codigo", "codigo IN ('ADMINISTRADOR', 'PROFESIONAL')");
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    cambio_contrasena_requerido = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    security_stamp = table.Column<string>(type: "text", nullable: false),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                    table.CheckConstraint("ck_usuarios_access_failed_count", "access_failed_count >= 0");
                    table.CheckConstraint("ck_usuarios_user_name_not_blank", "length(btrim(user_name)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "roles_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_claims_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profesionales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profesionales", x => x.id);
                    table.CheckConstraint("ck_profesionales_nombre_not_blank", "length(btrim(nombre)) > 0");
                    table.ForeignKey(
                        name: "FK_profesionales_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sesiones_auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sid = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_refresh_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    creada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ultimo_uso_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expira_inactividad_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_absoluta_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revocada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesiones_auth", x => x.id);
                    table.CheckConstraint("ck_sesiones_auth_expiraciones", "expira_inactividad_at <= expira_absoluta_at");
                    table.ForeignKey(
                        name: "FK_sesiones_auth_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuarios_claims_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_usuarios_logins_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_roles",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asignado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_roles", x => new { x.usuario_id, x.rol_id });
                    table.ForeignKey(
                        name: "FK_usuarios_roles_roles_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuarios_roles_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_tokens",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    nombre = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_tokens", x => new { x.usuario_id, x.login_provider, x.nombre });
                    table.ForeignKey(
                        name: "FK_usuarios_tokens_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "periodos_mensuales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profesional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anio = table.Column<short>(type: "smallint", nullable: false),
                    mes = table.Column<short>(type: "smallint", nullable: false),
                    retencion_porcentaje_aplicado = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    total_horas = table.Column<long>(type: "bigint", nullable: false),
                    bruto_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    retencion_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    liquido_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodos_mensuales", x => x.id);
                    table.UniqueConstraint("AK_periodos_mensuales_id_profesional_id_anio", x => new { x.id, x.profesional_id, x.anio });
                    table.CheckConstraint("ck_periodos_mensuales_json_exact", "total_horas <= 9007199254740991 AND bruto_total_clp <= 9007199254740991 AND retencion_total_clp <= 9007199254740991 AND liquido_total_clp <= 9007199254740991");
                    table.CheckConstraint("ck_periodos_mensuales_liquido_consistente", "retencion_total_clp <= bruto_total_clp AND liquido_total_clp = bruto_total_clp - retencion_total_clp");
                    table.CheckConstraint("ck_periodos_mensuales_mes", "mes BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_periodos_mensuales_retencion_aplicada", "retencion_porcentaje_aplicado BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_periodos_mensuales_totales_no_negativos", "total_horas >= 0 AND bruto_total_clp >= 0 AND retencion_total_clp >= 0 AND liquido_total_clp >= 0");
                    table.CheckConstraint("ck_periodos_mensuales_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_periodos_mensuales_profesionales_profesional_id",
                        column: x => x.profesional_id,
                        principalTable: "profesionales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_periodos_mensuales_retencion_boleta_honorarios_anio",
                        column: x => x.anio,
                        principalTable: "retencion_boleta_honorarios",
                        principalColumn: "anio",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profesionales_instituciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profesional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institucion_publica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profesionales_instituciones", x => x.id);
                    table.UniqueConstraint("AK_profesionales_instituciones_id_profesional_id", x => new { x.id, x.profesional_id });
                    table.ForeignKey(
                        name: "FK_profesionales_instituciones_instituciones_publicas_instituc~",
                        column: x => x.institucion_publica_id,
                        principalTable: "instituciones_publicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_profesionales_instituciones_profesionales_profesional_id",
                        column: x => x.profesional_id,
                        principalTable: "profesionales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sesiones_refresh_rotados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_refresh_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rotado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesiones_refresh_rotados", x => x.id);
                    table.ForeignKey(
                        name: "FK_sesiones_refresh_rotados_sesiones_auth_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones_auth",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tarifas_hora_anuales_versiones",
                columns: table => new
                {
                    profesional_institucion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anio = table.Column<short>(type: "smallint", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    valor_hora_clp = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarifas_hora_anuales_versiones", x => new { x.profesional_institucion_id, x.anio, x.version });
                    table.CheckConstraint("ck_tarifas_hora_anio", "anio BETWEEN 1900 AND 32767");
                    table.CheckConstraint("ck_tarifas_hora_clp_json_exact", "valor_hora_clp <= 9007199254740991");
                    table.CheckConstraint("ck_tarifas_hora_valor_no_negativo", "valor_hora_clp >= 0");
                    table.CheckConstraint("ck_tarifas_hora_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_tarifas_hora_anuales_versiones_profesionales_instituciones_~",
                        column: x => x.profesional_institucion_id,
                        principalTable: "profesionales_instituciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "periodos_instituciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    periodo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profesional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anio = table.Column<short>(type: "smallint", nullable: false),
                    profesional_institucion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarifa_hora_version = table.Column<int>(type: "integer", nullable: false),
                    total_horas = table.Column<long>(type: "bigint", nullable: false),
                    bruto_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    retencion_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    liquido_total_clp = table.Column<long>(type: "bigint", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodos_instituciones", x => x.id);
                    table.CheckConstraint("ck_periodos_instituciones_json_exact", "total_horas <= 9007199254740991 AND bruto_total_clp <= 9007199254740991 AND retencion_total_clp <= 9007199254740991 AND liquido_total_clp <= 9007199254740991");
                    table.CheckConstraint("ck_periodos_instituciones_liquido_consistente", "retencion_total_clp <= bruto_total_clp AND liquido_total_clp = bruto_total_clp - retencion_total_clp");
                    table.CheckConstraint("ck_periodos_instituciones_orden", "orden >= 0");
                    table.CheckConstraint("ck_periodos_instituciones_totales_no_negativos", "total_horas >= 0 AND bruto_total_clp >= 0 AND retencion_total_clp >= 0 AND liquido_total_clp >= 0");
                    table.CheckConstraint("ck_periodos_instituciones_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_periodos_instituciones_periodos_mensuales_periodo_id_profes~",
                        columns: x => new { x.periodo_id, x.profesional_id, x.anio },
                        principalTable: "periodos_mensuales",
                        principalColumns: new[] { "id", "profesional_id", "anio" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_periodos_instituciones_profesionales_instituciones_profesio~",
                        columns: x => new { x.profesional_institucion_id, x.profesional_id },
                        principalTable: "profesionales_instituciones",
                        principalColumns: new[] { "id", "profesional_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_periodos_instituciones_tarifas_hora_anuales_versiones_profe~",
                        columns: x => new { x.profesional_institucion_id, x.anio, x.tarifa_hora_version },
                        principalTable: "tarifas_hora_anuales_versiones",
                        principalColumns: new[] { "profesional_institucion_id", "anio", "version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registros_horas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    periodo_institucion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    horas = table.Column<int>(type: "integer", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_horas", x => x.id);
                    table.CheckConstraint("ck_registros_horas_minimo_uno", "horas >= 1");
                    table.CheckConstraint("ck_registros_horas_orden", "orden >= 0");
                    table.CheckConstraint("ck_registros_horas_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_registros_horas_periodos_instituciones_periodo_institucion_~",
                        column: x => x.periodo_institucion_id,
                        principalTable: "periodos_instituciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_instituciones_publicas_activa_nombre",
                table: "instituciones_publicas",
                columns: new[] { "activa", "nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_periodos_instituciones_periodo_id_profesional_id_anio",
                table: "periodos_instituciones",
                columns: new[] { "periodo_id", "profesional_id", "anio" });

            migrationBuilder.CreateIndex(
                name: "IX_periodos_instituciones_periodo_id_profesional_institucion_id",
                table: "periodos_instituciones",
                columns: new[] { "periodo_id", "profesional_institucion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_periodos_instituciones_profesional_id_anio",
                table: "periodos_instituciones",
                columns: new[] { "profesional_id", "anio" });

            migrationBuilder.CreateIndex(
                name: "IX_periodos_instituciones_profesional_institucion_id_anio_tari~",
                table: "periodos_instituciones",
                columns: new[] { "profesional_institucion_id", "anio", "tarifa_hora_version" });

            migrationBuilder.CreateIndex(
                name: "IX_periodos_instituciones_profesional_institucion_id_profesion~",
                table: "periodos_instituciones",
                columns: new[] { "profesional_institucion_id", "profesional_id" });

            migrationBuilder.CreateIndex(
                name: "IX_periodos_mensuales_anio",
                table: "periodos_mensuales",
                column: "anio");

            migrationBuilder.CreateIndex(
                name: "IX_periodos_mensuales_profesional_id_anio_mes",
                table: "periodos_mensuales",
                columns: new[] { "profesional_id", "anio", "mes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profesionales_usuario_id",
                table: "profesionales",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profesionales_instituciones_institucion_publica_id",
                table: "profesionales_instituciones",
                column: "institucion_publica_id");

            migrationBuilder.CreateIndex(
                name: "IX_profesionales_instituciones_profesional_id_activa",
                table: "profesionales_instituciones",
                columns: new[] { "profesional_id", "activa" });

            migrationBuilder.CreateIndex(
                name: "IX_profesionales_instituciones_profesional_id_institucion_publ~",
                table: "profesionales_instituciones",
                columns: new[] { "profesional_id", "institucion_publica_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registros_horas_periodo_institucion_id_orden",
                table: "registros_horas",
                columns: new[] { "periodo_institucion_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_codigo",
                table: "roles",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_claims_rol_id",
                table: "roles_claims",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_auth_refresh_token_hash",
                table: "sesiones_auth",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_auth_sid",
                table: "sesiones_auth",
                column: "sid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_auth_usuario_id_expira_absoluta_at",
                table: "sesiones_auth",
                columns: new[] { "usuario_id", "expira_absoluta_at" },
                filter: "revocada_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_refresh_rotados_familia_refresh_id",
                table: "sesiones_refresh_rotados",
                column: "familia_refresh_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_refresh_rotados_refresh_token_hash",
                table: "sesiones_refresh_rotados",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_refresh_rotados_sesion_id",
                table: "sesiones_refresh_rotados",
                column: "sesion_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "usuarios",
                column: "normalized_email",
                unique: true,
                filter: "normalized_email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "usuarios",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_claims_usuario_id",
                table: "usuarios_claims",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_logins_usuario_id",
                table: "usuarios_logins",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_roles_rol_id",
                table: "usuarios_roles",
                column: "rol_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registros_horas");

            migrationBuilder.DropTable(
                name: "roles_claims");

            migrationBuilder.DropTable(
                name: "sesiones_refresh_rotados");

            migrationBuilder.DropTable(
                name: "usuarios_claims");

            migrationBuilder.DropTable(
                name: "usuarios_logins");

            migrationBuilder.DropTable(
                name: "usuarios_roles");

            migrationBuilder.DropTable(
                name: "usuarios_tokens");

            migrationBuilder.DropTable(
                name: "periodos_instituciones");

            migrationBuilder.DropTable(
                name: "sesiones_auth");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "periodos_mensuales");

            migrationBuilder.DropTable(
                name: "tarifas_hora_anuales_versiones");

            migrationBuilder.DropTable(
                name: "retencion_boleta_honorarios");

            migrationBuilder.DropTable(
                name: "profesionales_instituciones");

            migrationBuilder.DropTable(
                name: "instituciones_publicas");

            migrationBuilder.DropTable(
                name: "profesionales");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
