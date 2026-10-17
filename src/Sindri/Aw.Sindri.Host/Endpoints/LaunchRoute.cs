using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Binds the launch routes' raw values (P1 binding rule): a '{launchId}' segment, a 400 SND-CLI-003 when malformed.</summary>
internal static class LaunchRoute
{
    /// <summary>The launch in a '{launchId}' segment.</summary>
    /// <param name="launchId">The segment.</param>
    /// <returns>The id, or SND-CLI-003.</returns>
    internal static Result<LaunchId> Launch(string launchId) =>
        LaunchId.TryParse(launchId, provider: null, out var id) ? id : SindriErrors.InvalidInput.With("'" + SafeText.Line(SafeText.Escape(launchId), 80) + "' isn't a launch id (LCH-…)");
}
