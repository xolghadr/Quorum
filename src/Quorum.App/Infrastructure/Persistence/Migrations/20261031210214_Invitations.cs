using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quorum.App.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Invitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailKey = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    InvitedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeclinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.CheckConstraint("CK_Invitations_one_outcome", "(CASE WHEN \"AcceptedAt\" IS NULL THEN 0 ELSE 1 END\n + CASE WHEN \"DeclinedAt\" IS NULL THEN 0 ELSE 1 END\n + CASE WHEN \"CancelledAt\" IS NULL THEN 0 ELSE 1 END) <= 1");
                    table.ForeignKey(
                        name: "FK_Invitations_Accounts_InvitedByAccountId",
                        column: x => x.InvitedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_GroupId_EmailKey",
                table: "Invitations",
                columns: new[] { "GroupId", "EmailKey" },
                unique: true,
                filter: "\"AcceptedAt\" IS NULL AND \"DeclinedAt\" IS NULL AND \"CancelledAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InvitedByAccountId",
                table: "Invitations",
                column: "InvitedByAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations");
        }
    }
}
