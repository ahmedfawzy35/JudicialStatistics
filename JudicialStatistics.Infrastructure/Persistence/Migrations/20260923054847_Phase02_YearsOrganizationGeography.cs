using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase02_YearsOrganizationGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeographicAreas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentAreaId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AreaKind = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeographicAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeographicAreas_GeographicAreas_ParentAreaId",
                        column: x => x.ParentAreaId,
                        principalTable: "GeographicAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Judges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    FullNameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Judges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JudicialRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudicialRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JudicialYears",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportingScopeId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudicialYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JudicialYears_ReportingScopes_ReportingScopeId",
                        column: x => x.ReportingScopeId,
                        principalTable: "ReportingScopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Circuits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JudicialYearId = table.Column<long>(type: "bigint", nullable: false),
                    CourtUnitId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Circuits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Circuits_CourtUnits_CourtUnitId",
                        column: x => x.CourtUnitId,
                        principalTable: "CourtUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Circuits_JudicialYears_JudicialYearId",
                        column: x => x.JudicialYearId,
                        principalTable: "JudicialYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JudicialYearId = table.Column<long>(type: "bigint", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    DecisionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DecisionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    ApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationVersions_AppUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationVersions_JudicialYears_JudicialYearId",
                        column: x => x.JudicialYearId,
                        principalTable: "JudicialYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatisticalPeriods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JudicialYearId = table.Column<long>(type: "bigint", nullable: false),
                    MonthStartDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatisticalPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatisticalPeriods_JudicialYears_JudicialYearId",
                        column: x => x.JudicialYearId,
                        principalTable: "JudicialYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CircuitId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Circuits_CircuitId",
                        column: x => x.CircuitId,
                        principalTable: "Circuits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CircuitVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CircuitId = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationVersionId = table.Column<long>(type: "bigint", nullable: false),
                    CircuitKind = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    MinJudges = table.Column<short>(type: "smallint", nullable: false),
                    MaxJudges = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircuitVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CircuitVersions_Circuits_CircuitId",
                        column: x => x.CircuitId,
                        principalTable: "Circuits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CircuitVersions_OrganizationVersions_OrganizationVersionId",
                        column: x => x.OrganizationVersionId,
                        principalTable: "OrganizationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CircuitJudgeAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CircuitVersionId = table.Column<long>(type: "bigint", nullable: false),
                    JudgeId = table.Column<long>(type: "bigint", nullable: false),
                    JudicialRoleId = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircuitJudgeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CircuitJudgeAssignments_CircuitVersions_CircuitVersionId",
                        column: x => x.CircuitVersionId,
                        principalTable: "CircuitVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CircuitJudgeAssignments_Judges_JudgeId",
                        column: x => x.JudgeId,
                        principalTable: "Judges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CircuitJudgeAssignments_JudicialRoles_JudicialRoleId",
                        column: x => x.JudicialRoleId,
                        principalTable: "JudicialRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    CircuitVersionId = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduleDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionVersions_CircuitVersions_CircuitVersionId",
                        column: x => x.CircuitVersionId,
                        principalTable: "CircuitVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionVersions_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionJurisdictions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    GeographicAreaId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionJurisdictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionJurisdictions_GeographicAreas_GeographicAreaId",
                        column: x => x.GeographicAreaId,
                        principalTable: "GeographicAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionJurisdictions_SessionVersions_SessionVersionId",
                        column: x => x.SessionVersionId,
                        principalTable: "SessionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CircuitJudgeAssignments_CircuitVersionId",
                table: "CircuitJudgeAssignments",
                column: "CircuitVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CircuitJudgeAssignments_JudgeId",
                table: "CircuitJudgeAssignments",
                column: "JudgeId");

            migrationBuilder.CreateIndex(
                name: "IX_CircuitJudgeAssignments_JudicialRoleId",
                table: "CircuitJudgeAssignments",
                column: "JudicialRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Circuits_CourtUnitId",
                table: "Circuits",
                column: "CourtUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Circuits_JudicialYearId",
                table: "Circuits",
                column: "JudicialYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Circuits_JudicialYearId_CourtUnitId_Code",
                table: "Circuits",
                columns: new[] { "JudicialYearId", "CourtUnitId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CircuitVersions_CircuitId",
                table: "CircuitVersions",
                column: "CircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_CircuitVersions_OrganizationVersionId",
                table: "CircuitVersions",
                column: "OrganizationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_GeographicAreas_Code",
                table: "GeographicAreas",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeographicAreas_ParentAreaId",
                table: "GeographicAreas",
                column: "ParentAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Judges_Code",
                table: "Judges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JudicialRoles_Code",
                table: "JudicialRoles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JudicialYears_ReportingScopeId",
                table: "JudicialYears",
                column: "ReportingScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationVersions_ApprovedByUserId",
                table: "OrganizationVersions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationVersions_JudicialYearId_VersionNo",
                table: "OrganizationVersions",
                columns: new[] { "JudicialYearId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionJurisdictions_GeographicAreaId",
                table: "SessionJurisdictions",
                column: "GeographicAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionJurisdictions_SessionVersionId_GeographicAreaId",
                table: "SessionJurisdictions",
                columns: new[] { "SessionVersionId", "GeographicAreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CircuitId",
                table: "Sessions",
                column: "CircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CircuitId_Code",
                table: "Sessions",
                columns: new[] { "CircuitId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionVersions_CircuitVersionId",
                table: "SessionVersions",
                column: "CircuitVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionVersions_SessionId",
                table: "SessionVersions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticalPeriods_JudicialYearId_MonthStartDate",
                table: "StatisticalPeriods",
                columns: new[] { "JudicialYearId", "MonthStartDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessionGrants_Sessions_SessionId",
                table: "UserSessionGrants",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSessionGrants_Sessions_SessionId",
                table: "UserSessionGrants");

            migrationBuilder.DropTable(
                name: "CircuitJudgeAssignments");

            migrationBuilder.DropTable(
                name: "SessionJurisdictions");

            migrationBuilder.DropTable(
                name: "StatisticalPeriods");

            migrationBuilder.DropTable(
                name: "Judges");

            migrationBuilder.DropTable(
                name: "JudicialRoles");

            migrationBuilder.DropTable(
                name: "GeographicAreas");

            migrationBuilder.DropTable(
                name: "SessionVersions");

            migrationBuilder.DropTable(
                name: "CircuitVersions");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "OrganizationVersions");

            migrationBuilder.DropTable(
                name: "Circuits");

            migrationBuilder.DropTable(
                name: "JudicialYears");
        }
    }
}
