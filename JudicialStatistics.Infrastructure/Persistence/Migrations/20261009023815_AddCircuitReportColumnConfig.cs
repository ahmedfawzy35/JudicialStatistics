using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCircuitReportColumnConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CircuitReportColumnConfigs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColumnKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultTitleAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomTitleAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    DisplayMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSystemRequired = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircuitReportColumnConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CircuitReportColumnConfigs");
        }
    }
}
