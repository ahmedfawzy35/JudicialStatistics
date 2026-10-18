using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatisticalDisciplines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatisticalDisciplines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CircuitKinds = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EntryFormKind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatisticalDisciplines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatisticalDisciplines_Code",
                table: "StatisticalDisciplines",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatisticalDisciplines");
        }
    }
}
