using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase06_JudgeJudgmentsAndPostponements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourcePeriodId",
                table: "IncomingRows",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JudgeJudgments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatisticsHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    JudgeId = table.Column<long>(type: "bigint", nullable: false),
                    CaseYearBandId = table.Column<long>(type: "bigint", nullable: false),
                    JudgmentKind = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgeJudgments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JudgeJudgments_CaseYearBands_CaseYearBandId",
                        column: x => x.CaseYearBandId,
                        principalTable: "CaseYearBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JudgeJudgments_Judges_JudgeId",
                        column: x => x.JudgeId,
                        principalTable: "Judges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JudgeJudgments_StatisticsHeaders_StatisticsHeaderId",
                        column: x => x.StatisticsHeaderId,
                        principalTable: "StatisticsHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Postponements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatisticsHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    CaseYearBandId = table.Column<long>(type: "bigint", nullable: false),
                    PostponementKind = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    TargetPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postponements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Postponements_CaseYearBands_CaseYearBandId",
                        column: x => x.CaseYearBandId,
                        principalTable: "CaseYearBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Postponements_StatisticalPeriods_TargetPeriodId",
                        column: x => x.TargetPeriodId,
                        principalTable: "StatisticalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Postponements_StatisticsHeaders_StatisticsHeaderId",
                        column: x => x.StatisticsHeaderId,
                        principalTable: "StatisticsHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncomingRows_SourcePeriodId",
                table: "IncomingRows",
                column: "SourcePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeJudgments_CaseYearBandId",
                table: "JudgeJudgments",
                column: "CaseYearBandId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeJudgments_JudgeId",
                table: "JudgeJudgments",
                column: "JudgeId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeJudgments_StatisticsHeaderId",
                table: "JudgeJudgments",
                column: "StatisticsHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeJudgments_StatisticsHeaderId_JudgeId",
                table: "JudgeJudgments",
                columns: new[] { "StatisticsHeaderId", "JudgeId" });

            migrationBuilder.CreateIndex(
                name: "IX_JudgeJudgments_StatisticsHeaderId_JudgeId_CaseYearBandId_JudgmentKind",
                table: "JudgeJudgments",
                columns: new[] { "StatisticsHeaderId", "JudgeId", "CaseYearBandId", "JudgmentKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JudgeJudgments_StatisticsHeaderId_JudgmentKind",
                table: "JudgeJudgments",
                columns: new[] { "StatisticsHeaderId", "JudgmentKind" });

            migrationBuilder.CreateIndex(
                name: "IX_Postponements_CaseYearBandId",
                table: "Postponements",
                column: "CaseYearBandId");

            migrationBuilder.CreateIndex(
                name: "IX_Postponements_StatisticsHeaderId",
                table: "Postponements",
                column: "StatisticsHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Postponements_StatisticsHeaderId_CaseYearBandId_PostponementKind_TargetPeriodId",
                table: "Postponements",
                columns: new[] { "StatisticsHeaderId", "CaseYearBandId", "PostponementKind", "TargetPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Postponements_StatisticsHeaderId_TargetPeriodId",
                table: "Postponements",
                columns: new[] { "StatisticsHeaderId", "TargetPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_Postponements_TargetPeriodId",
                table: "Postponements",
                column: "TargetPeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_IncomingRows_StatisticalPeriods_SourcePeriodId",
                table: "IncomingRows",
                column: "SourcePeriodId",
                principalTable: "StatisticalPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IncomingRows_StatisticalPeriods_SourcePeriodId",
                table: "IncomingRows");

            migrationBuilder.DropTable(
                name: "JudgeJudgments");

            migrationBuilder.DropTable(
                name: "Postponements");

            migrationBuilder.DropIndex(
                name: "IX_IncomingRows_SourcePeriodId",
                table: "IncomingRows");

            migrationBuilder.DropColumn(
                name: "SourcePeriodId",
                table: "IncomingRows");
        }
    }
}
