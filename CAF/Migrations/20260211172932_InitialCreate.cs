using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CAF.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContextData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    CoreFacts = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Availability = table.Column<int>(type: "integer", nullable: false),
                    IsUser = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    UseNextTurnOnly = table.Column<bool>(type: "boolean", nullable: false),
                    UseEveryTurn = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousAvailability = table.Column<int>(type: "integer", nullable: true),
                    TriggerKeywords = table.Column<string>(type: "text", nullable: true),
                    TriggerLookbackTurns = table.Column<int>(type: "integer", nullable: false),
                    TriggerMinMatchCount = table.Column<int>(type: "integer", nullable: false),
                    VectorId = table.Column<string>(type: "text", nullable: true),
                    EmbeddingUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InVectorDb = table.Column<bool>(type: "boolean", nullable: false),
                    SourceSessionId = table.Column<int>(type: "integer", nullable: true),
                    Speaker = table.Column<string>(type: "text", nullable: true),
                    Subtype = table.Column<string>(type: "text", nullable: true),
                    NonverbalBehavior = table.Column<string>(type: "text", nullable: true),
                    Path = table.Column<string>(type: "text", nullable: true),
                    RelevanceScore = table.Column<int>(type: "integer", nullable: false),
                    RelevanceReason = table.Column<string>(type: "text", nullable: true),
                    UsedLastOnTurnId = table.Column<int>(type: "integer", nullable: false),
                    CooldownTurns = table.Column<int>(type: "integer", nullable: false),
                    Display = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TriggerCount = table.Column<int>(type: "integer", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TokenCount = table.Column<int>(type: "integer", nullable: true),
                    TokenCountUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContextData_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Constant = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProfileId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flags_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProfileId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProfileId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settings_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemMessages_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Turns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Input = table.Column<string>(type: "text", nullable: false),
                    JsonInput = table.Column<string>(type: "text", nullable: false),
                    Response = table.Column<string>(type: "text", nullable: false),
                    StrippedTurn = table.Column<string>(type: "text", nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Turns_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LLMRequestLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: true),
                    SystemInstruction = table.Column<string>(type: "text", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    RawRequestJson = table.Column<string>(type: "text", nullable: true),
                    RawResponseJson = table.Column<string>(type: "text", nullable: true),
                    GeneratedText = table.Column<string>(type: "text", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    CachedContentTokenCount = table.Column<int>(type: "integer", nullable: false),
                    ThinkingTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    TurnId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LLMRequestLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LLMRequestLogs_Turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "Turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_Availability",
                table: "ContextData",
                column: "Availability");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_InVectorDb",
                table: "ContextData",
                column: "InVectorDb");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_IsArchived",
                table: "ContextData",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_IsEnabled",
                table: "ContextData",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_IsUser",
                table: "ContextData",
                column: "IsUser");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_ProfileId",
                table: "ContextData",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_ProfileId_Availability_IsEnabled_IsArchived",
                table: "ContextData",
                columns: ["ProfileId", "Availability", "IsEnabled", "IsArchived"]);

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_ProfileId_Type_Availability",
                table: "ContextData",
                columns: ["ProfileId", "Type", "Availability"]);

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_ProfileId_Type_InVectorDb",
                table: "ContextData",
                columns: ["ProfileId", "Type", "InVectorDb"]);

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_ProfileId_Type_IsUser",
                table: "ContextData",
                columns: ["ProfileId", "Type", "IsUser"]);

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_ProfileId_Type_SourceSessionId",
                table: "ContextData",
                columns: ["ProfileId", "Type", "SourceSessionId"]);

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_RelevanceScore",
                table: "ContextData",
                column: "RelevanceScore");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_SourceSessionId",
                table: "ContextData",
                column: "SourceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_Speaker",
                table: "ContextData",
                column: "Speaker");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_Type",
                table: "ContextData",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_UsedLastOnTurnId",
                table: "ContextData",
                column: "UsedLastOnTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_ContextData_VectorId",
                table: "ContextData",
                column: "VectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_Active",
                table: "Flags",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_Constant",
                table: "Flags",
                column: "Constant");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_CreatedAt",
                table: "Flags",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_ProfileId",
                table: "Flags",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_Value",
                table: "Flags",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_CachedContentTokenCount",
                table: "LLMRequestLogs",
                column: "CachedContentTokenCount");

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_DurationMs",
                table: "LLMRequestLogs",
                column: "DurationMs");

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_Model",
                table: "LLMRequestLogs",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_Provider",
                table: "LLMRequestLogs",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_Provider_Model_StartTime",
                table: "LLMRequestLogs",
                columns: ["Provider", "Model", "StartTime"]);

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_Provider_StartTime",
                table: "LLMRequestLogs",
                columns: ["Provider", "StartTime"]);

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_RequestId",
                table: "LLMRequestLogs",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_StartTime",
                table: "LLMRequestLogs",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_ThinkingTokens",
                table: "LLMRequestLogs",
                column: "ThinkingTokens");

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_TotalCost",
                table: "LLMRequestLogs",
                column: "TotalCost");

            migrationBuilder.CreateIndex(
                name: "IX_LLMRequestLogs_TurnId",
                table: "LLMRequestLogs",
                column: "TurnId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CreatedAt",
                table: "Sessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_IsActive",
                table: "Sessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Number",
                table: "Sessions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ProfileId",
                table: "Sessions",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_CreatedAt",
                table: "Settings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Name",
                table: "Settings",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_ProfileId",
                table: "Settings",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_CreatedAt",
                table: "SystemMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_IsActive",
                table: "SystemMessages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_IsArchived",
                table: "SystemMessages",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_Name",
                table: "SystemMessages",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_ParentId",
                table: "SystemMessages",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_ProfileId",
                table: "SystemMessages",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_Type",
                table: "SystemMessages",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_Type_IsActive",
                table: "SystemMessages",
                columns: ["Type", "IsActive"]);

            migrationBuilder.CreateIndex(
                name: "IX_SystemMessages_Type_IsActive_IsArchived",
                table: "SystemMessages",
                columns: ["Type", "IsActive", "IsArchived"]);

            migrationBuilder.CreateIndex(
                name: "IX_Turns_Accepted",
                table: "Turns",
                column: "Accepted");

            migrationBuilder.CreateIndex(
                name: "IX_Turns_CreatedAt",
                table: "Turns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Turns_SessionId",
                table: "Turns",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Turns_SessionId_CreatedAt",
                table: "Turns",
                columns: ["SessionId", "CreatedAt"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContextData");

            migrationBuilder.DropTable(
                name: "Flags");

            migrationBuilder.DropTable(
                name: "LLMRequestLogs");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "SystemMessages");

            migrationBuilder.DropTable(
                name: "Turns");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}
