using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase04_Statistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatisticsHeaders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    StatisticalPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    RuleSetVersionId = table.Column<long>(type: "bigint", nullable: false),
                    CaseYearPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatisticsHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatisticsHeaders_AppUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatisticsHeaders_AppUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatisticsHeaders_AppUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatisticsHeaders_AppUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatisticsHeaders_CaseYearPolicies_CaseYearPolicyId",
                        column: x => x.CaseYearPolicyId,
                        principalTable: "CaseYearPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatisticsHeaders_RuleSetVersions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalTable: "RuleSetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatisticsHeaders_SessionVersions_SessionVersionId",
                        column: x => x.SessionVersionId,
                        principalTable: "SessionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatisticsHeaders_StatisticalPeriods_StatisticalPeriodId",
                        column: x => x.StatisticalPeriodId,
                        principalTable: "StatisticalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatisticsHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntries_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEntries_StatisticsHeaders_StatisticsHeaderId",
                        column: x => x.StatisticsHeaderId,
                        principalTable: "StatisticsHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncomingRows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatisticsHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false),
                    CaseYearBandId = table.Column<long>(type: "bigint", nullable: false),
                    IncomingTypeId = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomingRows_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncomingRows_CaseYearBands_CaseYearBandId",
                        column: x => x.CaseYearBandId,
                        principalTable: "CaseYearBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncomingRows_IncomingTypes_IncomingTypeId",
                        column: x => x.IncomingTypeId,
                        principalTable: "IncomingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncomingRows_StatisticsHeaders_StatisticsHeaderId",
                        column: x => x.StatisticsHeaderId,
                        principalTable: "StatisticsHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpeningBalances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatisticsHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false),
                    CaseYearBandId = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_CaseYearBands_CaseYearBandId",
                        column: x => x.CaseYearBandId,
                        principalTable: "CaseYearBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_StatisticsHeaders_StatisticsHeaderId",
                        column: x => x.StatisticsHeaderId,
                        principalTable: "StatisticsHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatisticsHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false),
                    CaseYearBandId = table.Column<long>(type: "bigint", nullable: false),
                    ProductionItemTypeId = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionRows_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRows_CaseYearBands_CaseYearBandId",
                        column: x => x.CaseYearBandId,
                        principalTable: "CaseYearBands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRows_ProductionItemTypes_ProductionItemTypeId",
                        column: x => x.ProductionItemTypeId,
                        principalTable: "ProductionItemTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRows_StatisticsHeaders_StatisticsHeaderId",
                        column: x => x.StatisticsHeaderId,
                        principalTable: "StatisticsHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JudgmentAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionRowId = table.Column<long>(type: "bigint", nullable: false),
                    JudgeId = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgmentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JudgmentAllocations_Judges_JudgeId",
                        column: x => x.JudgeId,
                        principalTable: "Judges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JudgmentAllocations_ProductionRows_ProductionRowId",
                        column: x => x.ProductionRowId,
                        principalTable: "ProductionRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OccurredAt",
                table: "AuditEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_StatisticsHeaderId",
                table: "AuditEntries",
                column: "StatisticsHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_UserId",
                table: "AuditEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingRows_CaseTypeId",
                table: "IncomingRows",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingRows_CaseYearBandId",
                table: "IncomingRows",
                column: "CaseYearBandId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingRows_IncomingTypeId",
                table: "IncomingRows",
                column: "IncomingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingRows_StatisticsHeaderId",
                table: "IncomingRows",
                column: "StatisticsHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingRows_StatisticsHeaderId_CaseTypeId_CaseYearBandId_IncomingTypeId",
                table: "IncomingRows",
                columns: new[] { "StatisticsHeaderId", "CaseTypeId", "CaseYearBandId", "IncomingTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JudgmentAllocations_JudgeId",
                table: "JudgmentAllocations",
                column: "JudgeId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgmentAllocations_ProductionRowId",
                table: "JudgmentAllocations",
                column: "ProductionRowId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgmentAllocations_ProductionRowId_JudgeId",
                table: "JudgmentAllocations",
                columns: new[] { "ProductionRowId", "JudgeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_CaseTypeId",
                table: "OpeningBalances",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_CaseYearBandId",
                table: "OpeningBalances",
                column: "CaseYearBandId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_StatisticsHeaderId",
                table: "OpeningBalances",
                column: "StatisticsHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_StatisticsHeaderId_CaseTypeId_CaseYearBandId",
                table: "OpeningBalances",
                columns: new[] { "StatisticsHeaderId", "CaseTypeId", "CaseYearBandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRows_CaseTypeId",
                table: "ProductionRows",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRows_CaseYearBandId",
                table: "ProductionRows",
                column: "CaseYearBandId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRows_ProductionItemTypeId",
                table: "ProductionRows",
                column: "ProductionItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRows_StatisticsHeaderId",
                table: "ProductionRows",
                column: "StatisticsHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRows_StatisticsHeaderId_CaseTypeId_CaseYearBandId_ProductionItemTypeId",
                table: "ProductionRows",
                columns: new[] { "StatisticsHeaderId", "CaseTypeId", "CaseYearBandId", "ProductionItemTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_ApprovedByUserId",
                table: "StatisticsHeaders",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_CaseYearPolicyId",
                table: "StatisticsHeaders",
                column: "CaseYearPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_CreatedByUserId",
                table: "StatisticsHeaders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_ReviewedByUserId",
                table: "StatisticsHeaders",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_RuleSetVersionId",
                table: "StatisticsHeaders",
                column: "RuleSetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_SessionVersionId",
                table: "StatisticsHeaders",
                column: "SessionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_SessionVersionId_StatisticalPeriodId",
                table: "StatisticsHeaders",
                columns: new[] { "SessionVersionId", "StatisticalPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_StatisticalPeriodId",
                table: "StatisticsHeaders",
                column: "StatisticalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_Status",
                table: "StatisticsHeaders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StatisticsHeaders_SubmittedByUserId",
                table: "StatisticsHeaders",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "IncomingRows");

            migrationBuilder.DropTable(
                name: "JudgmentAllocations");

            migrationBuilder.DropTable(
                name: "OpeningBalances");

            migrationBuilder.DropTable(
                name: "ProductionRows");

            migrationBuilder.DropTable(
                name: "StatisticsHeaders");
        }
    }
}
