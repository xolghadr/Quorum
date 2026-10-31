using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quorum.App.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaitingSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT "GroupId"
                        FROM "Sessions"
                        WHERE "FacilitatorMembershipId" IS NULL
                        GROUP BY "GroupId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'A group already has more than one session waiting for its election.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_GroupId_Waiting",
                table: "Sessions",
                column: "GroupId",
                unique: true,
                filter: "\"FacilitatorMembershipId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_GroupId_Waiting",
                table: "Sessions");
        }
    }
}
