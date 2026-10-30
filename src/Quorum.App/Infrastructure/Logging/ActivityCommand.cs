using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Infrastructure.Logging;

public static class ActivityCommand
{
    public static void RunActivityCommandIfPresent(this WebApplication app)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "activity");
        if (index < 0)
            return;

        var reference = index + 1 < args.Length ? args[index + 1] : "";
        var code = Execute(app.Services, reference, Console.Out);
        Environment.Exit(code);
    }

    public static int Execute(IServiceProvider services, string reference, TextWriter writer)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuorumDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        return Print(db, clock, reference, writer);
    }

    public static int Print(QuorumDbContext db, IClock clock, string reference, TextWriter writer)
    {
        var cutoff = clock.UtcNow - ActivityRetention.Keep;
        var rows = db.ActivityRecords
            .Where(row => row.Reference == reference && row.AcceptedAt >= cutoff)
            .OrderBy(row => row.AcceptedAt)
            .ThenBy(row => row.Attempt)
            .ToList();
        if (rows.Count == 0)
        {
            writer.WriteLine("not found");
            return 1;
        }

        foreach (var row in rows)
            writer.Write(Format(row));
        return 0;
    }

    public static string Format(ActivityRecord row)
    {
        var accepted = row.AcceptedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return string.Join('\n',
        [
            $"reference: {row.Reference}",
            $"attempt: {row.Attempt}",
            $"outcome: {row.Outcome}",
            $"accepted_at: {accepted}",
            $"duration_ms: {row.DurationMs}",
            $"storage_wait_ms: {row.StorageWaitMs}",
            $"account_id: {Show(row.AccountId)}",
            $"group_id: {Show(row.GroupId)}",
            $"session_id: {Show(row.SessionId)}",
            $"member_id: {Show(row.MemberId)}",
            $"tracks: {Show(row.Tracks)}",
            $"topics: {Show(row.Topics)}",
            $"designs: {Show(row.Designs)}",
            $"reason: {row.Reason ?? "-"}",
            "",
        ]);
    }

    private static string Show(Guid? value) => value?.ToString() ?? "-";

    private static string Show(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";
}
