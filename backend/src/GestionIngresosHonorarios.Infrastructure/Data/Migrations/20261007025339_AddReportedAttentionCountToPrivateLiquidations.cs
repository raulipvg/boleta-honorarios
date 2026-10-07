using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionIngresosHonorarios.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportedAttentionCountToPrivateLiquidations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "atenciones_reportadas_pdf",
                table: "liquidaciones_privadas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_liquidaciones_privadas_atenciones_reportadas",
                table: "liquidaciones_privadas",
                sql: "atenciones_reportadas_pdf IS NULL OR atenciones_reportadas_pdf >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_liquidaciones_privadas_atenciones_reportadas",
                table: "liquidaciones_privadas");

            migrationBuilder.DropColumn(
                name: "atenciones_reportadas_pdf",
                table: "liquidaciones_privadas");
        }
    }
}
