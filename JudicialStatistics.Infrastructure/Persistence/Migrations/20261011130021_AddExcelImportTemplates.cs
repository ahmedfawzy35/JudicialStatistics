using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExcelImportTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExcelImportTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisciplineKind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StatisticsSheetName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TashkeelSheetName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    TashkeelCircuitStartRow = table.Column<int>(type: "int", nullable: false),
                    TashkeelCircuitNoCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelCircuitNameCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelSessionStartRow = table.Column<int>(type: "int", nullable: false),
                    TashkeelSessionCircuitNoCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelSessionNoCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelSessionNameCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelSessionTypeCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelSessionDayCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelSessionStartBlockCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelSessionCourtCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelJudgeStartRow = table.Column<int>(type: "int", nullable: false),
                    TashkeelJudgeNoCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelJudgeNameCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelJudgeCircuitNoCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelJudgeRoleCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TashkeelJudgeStatusCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlockTotalRows = table.Column<int>(type: "int", nullable: false),
                    YearBandsCount = table.Column<int>(type: "int", nullable: false),
                    PrimaryDataRowOffset = table.Column<int>(type: "int", nullable: false),
                    SecondaryJudgesRowOffset = table.Column<int>(type: "int", nullable: false),
                    Judge1HeaderRowOffset = table.Column<int>(type: "int", nullable: false),
                    Judge3HeaderRowOffset = table.Column<int>(type: "int", nullable: false),
                    Judge1HeaderCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Judge2HeaderCol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Judge5HeaderCol = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcelImportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExcelImportColumnMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExcelImportTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ColumnLetter = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TargetItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetItemNameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    JudgeSlotIndex = table.Column<int>(type: "int", nullable: true),
                    TargetMonthOffset = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcelImportColumnMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExcelImportColumnMappings_ExcelImportTemplates_ExcelImportTemplateId",
                        column: x => x.ExcelImportTemplateId,
                        principalTable: "ExcelImportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExcelImportColumnMappings_ExcelImportTemplateId",
                table: "ExcelImportColumnMappings",
                column: "ExcelImportTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExcelImportColumnMappings");

            migrationBuilder.DropTable(
                name: "ExcelImportTemplates");
        }
    }
}
