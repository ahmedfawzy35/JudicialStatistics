using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase08_GeoAndJudgeMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "GeographicAreas",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "JudgeMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JudgeId = table.Column<long>(type: "bigint", nullable: false),
                    CircuitId = table.Column<long>(type: "bigint", nullable: true),
                    MovementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DecisionNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DecisionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgeMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JudgeMovements_Circuits_CircuitId",
                        column: x => x.CircuitId,
                        principalTable: "Circuits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JudgeMovements_Judges_JudgeId",
                        column: x => x.JudgeId,
                        principalTable: "Judges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JudgeMovements_CircuitId",
                table: "JudgeMovements",
                column: "CircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeMovements_DecisionDate",
                table: "JudgeMovements",
                column: "DecisionDate");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeMovements_JudgeId",
                table: "JudgeMovements",
                column: "JudgeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JudgeMovements");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "GeographicAreas");
        }
    }
}
