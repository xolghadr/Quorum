using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.App.Infrastructure.Logging;

namespace Quorum.App.Features.Status;

[AllowAnonymous]
public sealed class StatusModel : PageModel
{
    private readonly HealthMonitor _health;

    public StatusModel(HealthMonitor health)
    {
        _health = health;
    }

    public string Readiness { get; private set; } = "not ready";
    public string StoredData { get; private set; } = "down";
    public string Use { get; private set; } = "local use only";
    public string CheckedAt { get; private set; } = "-";

    public void OnGet()
    {
        var snapshot = _health.Snapshot();
        Readiness = snapshot.Readiness == "ready" ? "ready" : "not ready";
        StoredData = snapshot.StoredData == "reachable" ? "reachable" : "down";
        Use = snapshot.Use == "ready-for-a-group" ? "ready for a group" : "local use only";
        CheckedAt = snapshot.CheckedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "-";
    }
}
