using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJudgeMovementCircuitsAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FromCircuitId",
                table: "JudgeMovements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromCircuitOrderJson",
                table: "JudgeMovements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JudicialRoleId",
                table: "JudgeMovements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ToCircuitId",
                table: "JudgeMovements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToCircuitOrderJson",
                table: "JudgeMovements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JudgeMovements_FromCircuitId",
                table: "JudgeMovements",
                column: "FromCircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeMovements_JudicialRoleId",
                table: "JudgeMovements",
                column: "JudicialRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeMovements_ToCircuitId",
                table: "JudgeMovements",
                column: "ToCircuitId");

            migrationBuilder.AddForeignKey(
                name: "FK_JudgeMovements_Circuits_FromCircuitId",
                table: "JudgeMovements",
                column: "FromCircuitId",
                principalTable: "Circuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JudgeMovements_Circuits_ToCircuitId",
                table: "JudgeMovements",
                column: "ToCircuitId",
                principalTable: "Circuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JudgeMovements_JudicialRoles_JudicialRoleId",
                table: "JudgeMovements",
                column: "JudicialRoleId",
                principalTable: "JudicialRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JudgeMovements_Circuits_FromCircuitId",
                table: "JudgeMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_JudgeMovements_Circuits_ToCircuitId",
                table: "JudgeMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_JudgeMovements_JudicialRoles_JudicialRoleId",
                table: "JudgeMovements");

            migrationBuilder.DropIndex(
                name: "IX_JudgeMovements_FromCircuitId",
                table: "JudgeMovements");

            migrationBuilder.DropIndex(
                name: "IX_JudgeMovements_JudicialRoleId",
                table: "JudgeMovements");

            migrationBuilder.DropIndex(
                name: "IX_JudgeMovements_ToCircuitId",
                table: "JudgeMovements");

            migrationBuilder.DropColumn(
                name: "FromCircuitId",
                table: "JudgeMovements");

            migrationBuilder.DropColumn(
                name: "FromCircuitOrderJson",
                table: "JudgeMovements");

            migrationBuilder.DropColumn(
                name: "JudicialRoleId",
                table: "JudgeMovements");

            migrationBuilder.DropColumn(
                name: "ToCircuitId",
                table: "JudgeMovements");

            migrationBuilder.DropColumn(
                name: "ToCircuitOrderJson",
                table: "JudgeMovements");
        }
    }
}
