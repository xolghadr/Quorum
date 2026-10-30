using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quorum.App.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogPublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogPublications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Accounts_CreatorAccountId",
                        column: x => x.CreatorAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                    table.ForeignKey(
                        name: "fk_tracks_publication",
                        column: x => x.PublicationId,
                        principalTable: "CatalogPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkedDesigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkedDesigns", x => x.Id);
                    table.ForeignKey(
                        name: "fk_designs_publication",
                        column: x => x.PublicationId,
                        principalTable: "CatalogPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DisplayNameKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Memberships_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    FacilitatorMembershipId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    YouCan = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Topics_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionAttendance",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionAttendance", x => new { x.SessionId, x.MembershipId });
                    table.ForeignKey(
                        name: "FK_SessionAttendance_Memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionAttendance_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionVoters",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionVoters", x => new { x.SessionId, x.MembershipId });
                    table.ForeignKey(
                        name: "FK_SessionVoters_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionVotes",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateMembershipId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionVotes", x => new { x.SessionId, x.VoterMembershipId });
                    table.ForeignKey(
                        name: "FK_SessionVotes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionTopics",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionTopics", x => new { x.SessionId, x.Position });
                    table.ForeignKey(
                        name: "FK_SessionTopics_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionTopics_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TopicProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicProgress_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TopicProgress_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TopicSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicSections_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_CreatorAccountId",
                table: "Groups",
                column: "CreatorAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_GroupId_AccountId",
                table: "Memberships",
                columns: new[] { "GroupId", "AccountId" },
                unique: true,
                filter: "\"AccountId\" IS NOT NULL AND \"RemovedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_GroupId_DisplayNameKey",
                table: "Memberships",
                columns: new[] { "GroupId", "DisplayNameKey" },
                unique: true,
                filter: "\"RemovedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TopicId_Fingerprint",
                table: "Questions",
                columns: new[] { "TopicId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionAttendance_MembershipId",
                table: "SessionAttendance",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_GroupId_Number",
                table: "Sessions",
                columns: new[] { "GroupId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionTopics_SessionId_TopicId",
                table: "SessionTopics",
                columns: new[] { "SessionId", "TopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionTopics_TopicId",
                table: "SessionTopics",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicProgress_GroupId_TopicId",
                table: "TopicProgress",
                columns: new[] { "GroupId", "TopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicProgress_TopicId",
                table: "TopicProgress",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_Key",
                table: "Topics",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Topics_TrackId",
                table: "Topics",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicSections_TopicId_Key",
                table: "TopicSections",
                columns: new[] { "TopicId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Key",
                table: "Tracks",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_PublicationId",
                table: "Tracks",
                column: "PublicationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkedDesigns_Key",
                table: "WorkedDesigns",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkedDesigns_PublicationId",
                table: "WorkedDesigns",
                column: "PublicationId");

            migrationBuilder.Sql("""
                ALTER TABLE "Tracks" DROP CONSTRAINT "fk_tracks_publication";
                ALTER TABLE "Tracks" ADD CONSTRAINT "fk_tracks_publication"
                    FOREIGN KEY ("PublicationId") REFERENCES "CatalogPublications" ("Id")
                    DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE "WorkedDesigns" DROP CONSTRAINT "fk_designs_publication";
                ALTER TABLE "WorkedDesigns" ADD CONSTRAINT "fk_designs_publication"
                    FOREIGN KEY ("PublicationId") REFERENCES "CatalogPublications" ("Id")
                    DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "SessionAttendance");

            migrationBuilder.DropTable(
                name: "SessionTopics");

            migrationBuilder.DropTable(
                name: "SessionVoters");

            migrationBuilder.DropTable(
                name: "SessionVotes");

            migrationBuilder.DropTable(
                name: "TopicProgress");

            migrationBuilder.DropTable(
                name: "TopicSections");

            migrationBuilder.DropTable(
                name: "WorkedDesigns");

            migrationBuilder.DropTable(
                name: "Memberships");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "Tracks");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "CatalogPublications");
        }
    }
}
