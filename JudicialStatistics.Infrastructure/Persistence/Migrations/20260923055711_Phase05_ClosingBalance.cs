using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase05_ClosingBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClosingBalances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatisticsHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false),
                    CaseYearBandId = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    CarriedForward = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosingBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClosingBalances_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosingBalances_CaseYearBands_CaseYearBandId",
                        column: x => x.CaseYearBandId,
                        principalTable: "CaseYearBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosingBalances_StatisticsHeaders_StatisticsHeaderId",
                        column: x => x.StatisticsHeaderId,
                        principalTable: "StatisticsHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosingBalances_CaseTypeId",
                table: "ClosingBalances",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingBalances_CaseYearBandId",
                table: "ClosingBalances",
                column: "CaseYearBandId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingBalances_StatisticsHeaderId",
                table: "ClosingBalances",
                column: "StatisticsHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingBalances_StatisticsHeaderId_CaseTypeId_CaseYearBandId",
                table: "ClosingBalances",
                columns: new[] { "StatisticsHeaderId", "CaseTypeId", "CaseYearBandId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosingBalances");
        }
    }
}
