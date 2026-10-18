using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOpeningBalanceSessionVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpeningBalances_StatisticsHeaders_StatisticsHeaderId",
                table: "OpeningBalances");

            migrationBuilder.RenameColumn(
                name: "StatisticsHeaderId",
                table: "OpeningBalances",
                newName: "SessionVersionId");

            migrationBuilder.RenameIndex(
                name: "IX_OpeningBalances_StatisticsHeaderId_CaseTypeId_CaseYearBandId",
                table: "OpeningBalances",
                newName: "IX_OpeningBalances_SessionVersionId_CaseTypeId_CaseYearBandId");

            migrationBuilder.RenameIndex(
                name: "IX_OpeningBalances_StatisticsHeaderId",
                table: "OpeningBalances",
                newName: "IX_OpeningBalances_SessionVersionId");

            migrationBuilder.Sql(@"
                UPDATE ob
                SET ob.SessionVersionId = sh.SessionVersionId
                FROM [OpeningBalances] ob
                INNER JOIN [StatisticsHeaders] sh ON ob.SessionVersionId = sh.Id;

                DELETE FROM [OpeningBalances]
                WHERE [SessionVersionId] NOT IN (SELECT [Id] FROM [SessionVersions]);

                WITH CTE AS (
                    SELECT Id, ROW_NUMBER() OVER(PARTITION BY SessionVersionId, CaseTypeId, CaseYearBandId ORDER BY Id) as rn
                    FROM [OpeningBalances]
                )
                DELETE FROM CTE WHERE rn > 1;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_OpeningBalances_SessionVersions_SessionVersionId",
                table: "OpeningBalances",
                column: "SessionVersionId",
                principalTable: "SessionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpeningBalances_SessionVersions_SessionVersionId",
                table: "OpeningBalances");

            migrationBuilder.RenameColumn(
                name: "SessionVersionId",
                table: "OpeningBalances",
                newName: "StatisticsHeaderId");

            migrationBuilder.RenameIndex(
                name: "IX_OpeningBalances_SessionVersionId_CaseTypeId_CaseYearBandId",
                table: "OpeningBalances",
                newName: "IX_OpeningBalances_StatisticsHeaderId_CaseTypeId_CaseYearBandId");

            migrationBuilder.RenameIndex(
                name: "IX_OpeningBalances_SessionVersionId",
                table: "OpeningBalances",
                newName: "IX_OpeningBalances_StatisticsHeaderId");

            migrationBuilder.AddForeignKey(
                name: "FK_OpeningBalances_StatisticsHeaders_StatisticsHeaderId",
                table: "OpeningBalances",
                column: "StatisticsHeaderId",
                principalTable: "StatisticsHeaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
