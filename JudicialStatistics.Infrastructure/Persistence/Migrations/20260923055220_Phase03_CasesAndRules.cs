using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudicialStatistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase03_CasesAndRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseTaxonomyNodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTaxonomyNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseTaxonomyNodes_CaseTaxonomyNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "CaseTaxonomyNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeferralReasons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceKind = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeferralReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomingTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputSource = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JudgmentTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgmentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleSetVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    ApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSetVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleSetVersions_AppUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaxonomyNodeId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StatisticalLevelCode = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    ProcedureCode = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    AgeCategoryCode = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseTypes_CaseTaxonomyNodes_TaxonomyNodeId",
                        column: x => x.TaxonomyNodeId,
                        principalTable: "CaseTaxonomyNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JudgeActionTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeferralReasonId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgeActionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JudgeActionTypes_DeferralReasons_DeferralReasonId",
                        column: x => x.DeferralReasonId,
                        principalTable: "DeferralReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionItemTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemKind = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    EntryScreen = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    DeferralReasonId = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionItemTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionItemTypes_DeferralReasons_DeferralReasonId",
                        column: x => x.DeferralReasonId,
                        principalTable: "DeferralReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JudgmentTreeNodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleSetVersionId = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgmentTreeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JudgmentTreeNodes_JudgmentTreeNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "JudgmentTreeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JudgmentTreeNodes_RuleSetVersions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalTable: "RuleSetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseTypeDeferralRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleSetVersionId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false),
                    DeferralReasonId = table.Column<long>(type: "bigint", nullable: false),
                    ReturnIncomingTypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTypeDeferralRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseTypeDeferralRules_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeDeferralRules_DeferralReasons_DeferralReasonId",
                        column: x => x.DeferralReasonId,
                        principalTable: "DeferralReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeDeferralRules_IncomingTypes_ReturnIncomingTypeId",
                        column: x => x.ReturnIncomingTypeId,
                        principalTable: "IncomingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeDeferralRules_RuleSetVersions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalTable: "RuleSetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseYearPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JudicialYearId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    DecisionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    RuleSetVersionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseYearPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseYearPolicies_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseYearPolicies_JudicialYears_JudicialYearId",
                        column: x => x.JudicialYearId,
                        principalTable: "JudicialYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseYearPolicies_RuleSetVersions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalTable: "RuleSetVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SessionCaseTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionVersionId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCaseTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionCaseTypes_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionCaseTypes_SessionVersions_SessionVersionId",
                        column: x => x.SessionVersionId,
                        principalTable: "SessionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseTypeInputRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleSetVersionId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false),
                    IncomingTypeId = table.Column<long>(type: "bigint", nullable: true),
                    ProductionItemTypeId = table.Column<long>(type: "bigint", nullable: true),
                    JudgeActionTypeId = table.Column<long>(type: "bigint", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    DeferralReasonId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTypeInputRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseTypeInputRules_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeInputRules_DeferralReasons_DeferralReasonId",
                        column: x => x.DeferralReasonId,
                        principalTable: "DeferralReasons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CaseTypeInputRules_IncomingTypes_IncomingTypeId",
                        column: x => x.IncomingTypeId,
                        principalTable: "IncomingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeInputRules_JudgeActionTypes_JudgeActionTypeId",
                        column: x => x.JudgeActionTypeId,
                        principalTable: "JudgeActionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeInputRules_ProductionItemTypes_ProductionItemTypeId",
                        column: x => x.ProductionItemTypeId,
                        principalTable: "ProductionItemTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeInputRules_RuleSetVersions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalTable: "RuleSetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseTypeJudgmentRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleSetVersionId = table.Column<long>(type: "bigint", nullable: false),
                    CaseTypeId = table.Column<long>(type: "bigint", nullable: false),
                    JudgmentTypeId = table.Column<long>(type: "bigint", nullable: false),
                    TreeNodeId = table.Column<long>(type: "bigint", nullable: false),
                    CountsInJudgmentTotal = table.Column<bool>(type: "bit", nullable: false),
                    RequiresAllocation = table.Column<bool>(type: "bit", nullable: false),
                    DeferralReasonId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTypeJudgmentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseTypeJudgmentRules_CaseTypes_CaseTypeId",
                        column: x => x.CaseTypeId,
                        principalTable: "CaseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeJudgmentRules_DeferralReasons_DeferralReasonId",
                        column: x => x.DeferralReasonId,
                        principalTable: "DeferralReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeJudgmentRules_JudgmentTreeNodes_TreeNodeId",
                        column: x => x.TreeNodeId,
                        principalTable: "JudgmentTreeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeJudgmentRules_JudgmentTypes_JudgmentTypeId",
                        column: x => x.JudgmentTypeId,
                        principalTable: "JudgmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTypeJudgmentRules_RuleSetVersions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalTable: "RuleSetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseYearBands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseYearPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    FromYear = table.Column<short>(type: "smallint", nullable: true),
                    ToYear = table.Column<short>(type: "smallint", nullable: true),
                    BandKind = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    LabelAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseYearBands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseYearBands_CaseYearPolicies_CaseYearPolicyId",
                        column: x => x.CaseYearPolicyId,
                        principalTable: "CaseYearPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTaxonomyNodes_Code",
                table: "CaseTaxonomyNodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseTaxonomyNodes_ParentId",
                table: "CaseTaxonomyNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeDeferralRules_CaseTypeId",
                table: "CaseTypeDeferralRules",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeDeferralRules_DeferralReasonId",
                table: "CaseTypeDeferralRules",
                column: "DeferralReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeDeferralRules_ReturnIncomingTypeId",
                table: "CaseTypeDeferralRules",
                column: "ReturnIncomingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeDeferralRules_RuleSetVersionId_CaseTypeId",
                table: "CaseTypeDeferralRules",
                columns: new[] { "RuleSetVersionId", "CaseTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeDeferralRules_RuleSetVersionId_CaseTypeId_DeferralReasonId",
                table: "CaseTypeDeferralRules",
                columns: new[] { "RuleSetVersionId", "CaseTypeId", "DeferralReasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeInputRules_CaseTypeId",
                table: "CaseTypeInputRules",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeInputRules_DeferralReasonId",
                table: "CaseTypeInputRules",
                column: "DeferralReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeInputRules_IncomingTypeId",
                table: "CaseTypeInputRules",
                column: "IncomingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeInputRules_JudgeActionTypeId",
                table: "CaseTypeInputRules",
                column: "JudgeActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeInputRules_ProductionItemTypeId",
                table: "CaseTypeInputRules",
                column: "ProductionItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeInputRules_RuleSetVersionId_CaseTypeId",
                table: "CaseTypeInputRules",
                columns: new[] { "RuleSetVersionId", "CaseTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeJudgmentRules_CaseTypeId",
                table: "CaseTypeJudgmentRules",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeJudgmentRules_DeferralReasonId",
                table: "CaseTypeJudgmentRules",
                column: "DeferralReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeJudgmentRules_JudgmentTypeId",
                table: "CaseTypeJudgmentRules",
                column: "JudgmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeJudgmentRules_RuleSetVersionId",
                table: "CaseTypeJudgmentRules",
                column: "RuleSetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeJudgmentRules_RuleSetVersionId_CaseTypeId_JudgmentTypeId",
                table: "CaseTypeJudgmentRules",
                columns: new[] { "RuleSetVersionId", "CaseTypeId", "JudgmentTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypeJudgmentRules_TreeNodeId",
                table: "CaseTypeJudgmentRules",
                column: "TreeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypes_Code",
                table: "CaseTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseTypes_TaxonomyNodeId",
                table: "CaseTypes",
                column: "TaxonomyNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseYearBands_CaseYearPolicyId",
                table: "CaseYearBands",
                column: "CaseYearPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseYearPolicies_CaseTypeId",
                table: "CaseYearPolicies",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseYearPolicies_JudicialYearId",
                table: "CaseYearPolicies",
                column: "JudicialYearId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseYearPolicies_JudicialYearId_CaseTypeId_VersionNo",
                table: "CaseYearPolicies",
                columns: new[] { "JudicialYearId", "CaseTypeId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseYearPolicies_RuleSetVersionId",
                table: "CaseYearPolicies",
                column: "RuleSetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DeferralReasons_Code",
                table: "DeferralReasons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomingTypes_Code",
                table: "IncomingTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JudgeActionTypes_Code",
                table: "JudgeActionTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JudgeActionTypes_DeferralReasonId",
                table: "JudgeActionTypes",
                column: "DeferralReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgmentTreeNodes_ParentId",
                table: "JudgmentTreeNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgmentTreeNodes_RuleSetVersionId",
                table: "JudgmentTreeNodes",
                column: "RuleSetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgmentTypes_Code",
                table: "JudgmentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionItemTypes_Code",
                table: "ProductionItemTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionItemTypes_DeferralReasonId",
                table: "ProductionItemTypes",
                column: "DeferralReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleSetVersions_ApprovedByUserId",
                table: "RuleSetVersions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCaseTypes_CaseTypeId",
                table: "SessionCaseTypes",
                column: "CaseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCaseTypes_SessionVersionId_CaseTypeId",
                table: "SessionCaseTypes",
                columns: new[] { "SessionVersionId", "CaseTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseTypeDeferralRules");

            migrationBuilder.DropTable(
                name: "CaseTypeInputRules");

            migrationBuilder.DropTable(
                name: "CaseTypeJudgmentRules");

            migrationBuilder.DropTable(
                name: "CaseYearBands");

            migrationBuilder.DropTable(
                name: "SessionCaseTypes");

            migrationBuilder.DropTable(
                name: "IncomingTypes");

            migrationBuilder.DropTable(
                name: "JudgeActionTypes");

            migrationBuilder.DropTable(
                name: "ProductionItemTypes");

            migrationBuilder.DropTable(
                name: "JudgmentTreeNodes");

            migrationBuilder.DropTable(
                name: "JudgmentTypes");

            migrationBuilder.DropTable(
                name: "CaseYearPolicies");

            migrationBuilder.DropTable(
                name: "DeferralReasons");

            migrationBuilder.DropTable(
                name: "CaseTypes");

            migrationBuilder.DropTable(
                name: "RuleSetVersions");

            migrationBuilder.DropTable(
                name: "CaseTaxonomyNodes");
        }
    }
}
