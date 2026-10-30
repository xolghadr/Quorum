using Quorum.App.SharedKernel;

namespace Quorum.App.Infrastructure.Time;

public static class TimeExtensions
{
    public static WebApplicationBuilder AddQuorumTime(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IClock, SystemClock>();
        return builder;
    }
}
