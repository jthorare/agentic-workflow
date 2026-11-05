using Aw.Sindri.Core.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Views;

/// <summary>A <see cref="ProjectorProgress"/> as it stands after the projector's first pass found nothing to fold (#2053): hosts that never run a pass start from here, since a fresh one reads Rebuilding.</summary>
internal static class FirstPass
{
    /// <summary>A progress that has published an empty, healthy status.</summary>
    /// <param name="clock">The progress's clock.</param>
    /// <returns>The progress.</returns>
    internal static ProjectorProgress Progress(TimeProvider clock)
    {
        var progress = new ProjectorProgress(clock);
        Publish(progress);
        return progress;
    }

    /// <summary>Publishes the empty, healthy status into a built host's progress, as its first pass would.</summary>
    /// <param name="services">The built host's services.</param>
    internal static void Publish(IServiceProvider services) => Publish(services.GetRequiredService<ProjectorProgress>());

    private static void Publish(ProjectorProgress progress) =>
        progress.Publish(new ViewsStatus(0, 0, ProjectedAt: null, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null));
}
