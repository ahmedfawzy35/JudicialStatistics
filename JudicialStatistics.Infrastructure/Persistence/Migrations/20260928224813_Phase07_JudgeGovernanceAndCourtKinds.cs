using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase07_JudgeGovernanceAndCourtKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedCourtUnitId",
                table: "Judges",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Judges_CreatedCourtUnitId",
                table: "Judges",
                column: "CreatedCourtUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Judges_FullNameAr",
                table: "Judges",
                column: "FullNameAr",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Judges_CourtUnits_CreatedCourtUnitId",
                table: "Judges",
                column: "CreatedCourtUnitId",
                principalTable: "CourtUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Judges_CourtUnits_CreatedCourtUnitId",
                table: "Judges");

            migrationBuilder.DropIndex(
                name: "IX_Judges_CreatedCourtUnitId",
                table: "Judges");

            migrationBuilder.DropIndex(
                name: "IX_Judges_FullNameAr",
                table: "Judges");

            migrationBuilder.DropColumn(
                name: "CreatedCourtUnitId",
                table: "Judges");
        }
    }
}
