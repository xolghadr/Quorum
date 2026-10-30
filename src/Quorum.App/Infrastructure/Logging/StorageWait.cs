using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Quorum.App.Infrastructure.Logging;

public static class StorageWait
{
    private static readonly AsyncLocal<StrongBox<long>?> Current = new();

    public static void Reset() => Current.Value = new StrongBox<long>(0);

    public static void Add(TimeSpan elapsed)
    {
        var box = Current.Value;
        if (box is null)
        {
            box = new StrongBox<long>(0);
            Current.Value = box;
        }

        var milliseconds = (long)elapsed.TotalMilliseconds;
        if (milliseconds < 0)
            milliseconds = 0;
        box.Value += milliseconds;
    }

    public static long Milliseconds => Math.Max(0, Current.Value?.Value ?? 0);

    public static long Time(Stopwatch watch)
    {
        var elapsed = watch.ElapsedMilliseconds;
        return elapsed < 0 ? 0 : elapsed;
    }
}
