using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>'session wrap --launch &lt;id&gt; -- &lt;agent cli …&gt;' (API spec §8.1): the launch and the command to run under it.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Limits">The limits file ('--limits'), or null for an unwatched wrap.</param>
/// <param name="Argv">The agent's command line; the first element is the executable.</param>
internal sealed record SessionWrapArguments(LaunchId Launch, IReadOnlyList<string> Argv, string? Limits = null)
{
    internal const string Usage = "session wrap --launch <id> [--limits <file>] -- <agent cli …>";

    /// <summary>Reads the arguments after 'session wrap'.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The launch and command, or SND-CLI-003 for a missing or malformed part.</returns>
    internal static Result<SessionWrapArguments> Parse(string[] args)
    {
        string? limits = null;
        if (args is ["--launch", var launchId, "--limits", var file, .. var afterLimits])
        {
            (args, limits) = (["--launch", launchId, .. afterLimits], file);
        }

        if (args is not ["--launch", var id, "--", var bin, .. var rest])
        {
            return SindriErrors.InvalidInput.With("usage: sindri " + Usage);
        }

        return LaunchId.TryParse(id, provider: null, out var launch)
            ? new SessionWrapArguments(launch, [bin, .. rest], limits)
            : SindriErrors.InvalidInput.With("'" + id + "' isn't a launch id (LCH-…)");
    }
}
