using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quorum.App.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivityRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Attempt = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    StorageWaitMs = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tracks = table.Column<int>(type: "integer", nullable: true),
                    Topics = table.Column<int>(type: "integer", nullable: true),
                    Designs = table.Column<int>(type: "integer", nullable: true),
                    ExportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_records", x => x.Id);
                    table.CheckConstraint("CK_activity_records_counts", "(\"Attempt\" = 'publish-catalog' AND \"Outcome\" = 'succeeded') OR (\"Tracks\" IS NULL AND \"Topics\" IS NULL AND \"Designs\" IS NULL)");
                    table.CheckConstraint("CK_activity_records_duration", "\"DurationMs\" >= 0");
                    table.CheckConstraint("CK_activity_records_outcome", "\"Outcome\" IN ('succeeded', 'refused')");
                    table.CheckConstraint("CK_activity_records_reason", "\"Reason\" IS NULL OR \"Reason\" = 'unknown-account'");
                    table.CheckConstraint("CK_activity_records_storage_wait", "\"StorageWaitMs\" >= 0 AND \"StorageWaitMs\" <= \"DurationMs\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_records_AcceptedAt",
                table: "activity_records",
                column: "AcceptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_activity_records_ExportedAt",
                table: "activity_records",
                column: "ExportedAt",
                filter: "\"ExportedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_activity_records_Reference_Attempt",
                table: "activity_records",
                columns: new[] { "Reference", "Attempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_records");
        }
    }
}
