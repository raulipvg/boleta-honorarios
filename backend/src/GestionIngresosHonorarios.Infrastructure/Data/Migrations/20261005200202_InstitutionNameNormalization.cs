using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionIngresosHonorarios.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InstitutionNameNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION normalize_public_institution_name(input text)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                STRICT
                PARALLEL SAFE
                AS $function$
                    SELECT replace(
                        btrim(regexp_replace(
                            regexp_replace(
                                normalize(replace(lower(normalize(input, NFKC)), 'ñ', chr(57344)), NFKD),
                                '[' || chr(768) || '-' || chr(879)
                                    || chr(6832) || '-' || chr(6863)
                                    || chr(7616) || '-' || chr(7679)
                                    || chr(8400) || '-' || chr(8447)
                                    || chr(65056) || '-' || chr(65071) || ']',
                                '', 'g'),
                            '[^[:alnum:]' || chr(57344) || ']+', ' ', 'g')
                        ),
                        chr(57344), 'ñ'
                    );
                $function$;

                CREATE OR REPLACE FUNCTION normalize_public_institution_display_name(input text)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                STRICT
                PARALLEL SAFE
                AS $function$
                    SELECT btrim(regexp_replace(normalize(input, NFC), '[[:space:]]+', ' ', 'g'));
                $function$;

                UPDATE instituciones_publicas
                SET nombre = normalize_public_institution_display_name(nombre),
                    updated_at = now()
                WHERE nombre <> normalize_public_institution_display_name(nombre);
                """);

            migrationBuilder.AddColumn<string>(
                name: "nombre_normalizado",
                table: "instituciones_publicas",
                type: "text",
                nullable: false,
                computedColumnSql: "normalize_public_institution_name(nombre)",
                stored: true);

            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM instituciones_publicas
                        WHERE nombre_normalizado = ''
                    ) THEN
                        RAISE EXCEPTION 'Hay instituciones sin letras ni números; corrija esos nombres antes de aplicar la migración.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM profesionales_instituciones relation_a
                        JOIN instituciones_publicas institution_a
                          ON institution_a.id = relation_a.institucion_publica_id
                        JOIN profesionales_instituciones relation_b
                          ON relation_b.profesional_id = relation_a.profesional_id
                         AND relation_b.id <> relation_a.id
                        JOIN instituciones_publicas institution_b
                          ON institution_b.id = relation_b.institucion_publica_id
                         AND institution_b.nombre_normalizado = institution_a.nombre_normalizado
                         AND institution_b.id <> institution_a.id
                    ) THEN
                        RAISE EXCEPTION 'Hay profesionales relacionados con instituciones duplicadas; revise sus tarifas e historial antes de consolidar el catálogo.';
                    END IF;
                END
                $migration$;

                CREATE TEMP TABLE instituciones_publicas_duplicadas ON COMMIT DROP AS
                WITH relation_counts AS (
                    SELECT institucion_publica_id, count(*) AS relation_count
                    FROM profesionales_instituciones
                    GROUP BY institucion_publica_id
                ), ranked AS (
                    SELECT institution.id,
                           first_value(institution.id) OVER (
                               PARTITION BY institution.nombre_normalizado
                               ORDER BY institution.activa DESC,
                                        coalesce(relation_counts.relation_count, 0) DESC,
                                        institution.created_at,
                                        institution.id
                           ) AS canonical_id
                    FROM instituciones_publicas institution
                    LEFT JOIN relation_counts
                      ON relation_counts.institucion_publica_id = institution.id
                )
                SELECT id AS duplicate_id, canonical_id
                FROM ranked
                WHERE id <> canonical_id;

                UPDATE profesionales_instituciones relation
                SET institucion_publica_id = duplicate.canonical_id
                FROM instituciones_publicas_duplicadas duplicate
                WHERE relation.institucion_publica_id = duplicate.duplicate_id;

                DELETE FROM instituciones_publicas institution
                USING instituciones_publicas_duplicadas duplicate
                WHERE institution.id = duplicate.duplicate_id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_instituciones_publicas_nombre_normalizado",
                table: "instituciones_publicas",
                column: "nombre_normalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_instituciones_publicas_nombre_normalizado",
                table: "instituciones_publicas");

            migrationBuilder.DropColumn(
                name: "nombre_normalizado",
                table: "instituciones_publicas");

            migrationBuilder.Sql("DROP FUNCTION normalize_public_institution_name(text);");
            migrationBuilder.Sql("DROP FUNCTION normalize_public_institution_display_name(text);");
        }
    }
}
