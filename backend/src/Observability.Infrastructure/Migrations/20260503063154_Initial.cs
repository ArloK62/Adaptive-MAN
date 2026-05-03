using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Observability.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KeyType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundJobFailures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurrenceCount = table.Column<long>(type: "bigint", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSuppressedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleaseSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobFailures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Errors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FingerprintVersion = table.Column<int>(type: "int", nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EndpointGroup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    JobName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    NormalizedRoute = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ReleaseSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PropertiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurrenceCount = table.Column<long>(type: "bigint", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Errors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DistinctId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NormalizedRoute = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EndpointGroup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FeatureArea = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PropertiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReleaseSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SafetyViolations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RejectedField = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyViolations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DistinctId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HasError = table.Column<bool>(type: "bit", nullable: false),
                    ReleaseSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEnvironments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvironmentName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReplayEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowedOriginsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEnvironments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEnvironments_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEnvironments_ApplicationId_EnvironmentName",
                table: "AppEnvironments",
                columns: new[] { "ApplicationId", "EnvironmentName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Slug",
                table: "Applications",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobFailures_ApplicationId_EnvironmentId_Fingerprint",
                table: "BackgroundJobFailures",
                columns: new[] { "ApplicationId", "EnvironmentId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobFailures_ApplicationId_EnvironmentId_LastSeenAt",
                table: "BackgroundJobFailures",
                columns: new[] { "ApplicationId", "EnvironmentId", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Errors_ApplicationId_EnvironmentId_Fingerprint",
                table: "Errors",
                columns: new[] { "ApplicationId", "EnvironmentId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Errors_ApplicationId_EnvironmentId_LastSeenAt",
                table: "Errors",
                columns: new[] { "ApplicationId", "EnvironmentId", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_ApplicationId_EnvironmentId_CreatedAt",
                table: "Events",
                columns: new[] { "ApplicationId", "EnvironmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_ApplicationId_EventName_CreatedAt",
                table: "Events",
                columns: new[] { "ApplicationId", "EventName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyViolations_ApplicationId_EnvironmentId_CreatedAt",
                table: "SafetyViolations",
                columns: new[] { "ApplicationId", "EnvironmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ApplicationId_EnvironmentId_LastSeenAt",
                table: "Sessions",
                columns: new[] { "ApplicationId", "EnvironmentId", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ApplicationId_EnvironmentId_SessionId",
                table: "Sessions",
                columns: new[] { "ApplicationId", "EnvironmentId", "SessionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AppEnvironments");

            migrationBuilder.DropTable(
                name: "BackgroundJobFailures");

            migrationBuilder.DropTable(
                name: "Errors");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "SafetyViolations");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
