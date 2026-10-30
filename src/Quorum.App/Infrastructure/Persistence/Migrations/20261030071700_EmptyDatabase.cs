using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Infrastructure.Persistence.Migrations;

[DbContext(typeof(QuorumDbContext))]
[Migration("20261030071700_EmptyDatabase")]
public partial class EmptyDatabase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
