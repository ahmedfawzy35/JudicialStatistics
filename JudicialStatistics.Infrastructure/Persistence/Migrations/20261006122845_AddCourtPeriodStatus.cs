using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtPeriodStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourtPeriodStatuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourtUnitId = table.Column<long>(type: "bigint", nullable: false),
                    StatisticalPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    IsOpenForEntry = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtPeriodStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtPeriodStatuses_AppUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourtPeriodStatuses_CourtUnits_CourtUnitId",
                        column: x => x.CourtUnitId,
                        principalTable: "CourtUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourtPeriodStatuses_StatisticalPeriods_StatisticalPeriodId",
                        column: x => x.StatisticalPeriodId,
                        principalTable: "StatisticalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourtPeriodStatuses_CourtUnitId_StatisticalPeriodId",
                table: "CourtPeriodStatuses",
                columns: new[] { "CourtUnitId", "StatisticalPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourtPeriodStatuses_StatisticalPeriodId",
                table: "CourtPeriodStatuses",
                column: "StatisticalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPeriodStatuses_UpdatedByUserId",
                table: "CourtPeriodStatuses",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourtPeriodStatuses");
        }
    }
}
