using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri index embed [pause|resume]' (owned-index spec §5.6) as the 'services' command it is: no word runs the 'index-embed' service now, 'pause' and 'resume' use the scheduler's own pause.</summary>
internal static class IndexEmbedArguments
{
    internal const string Usage = "index embed [--detach] [--json] [--no-start] | index embed pause|resume [--no-start]";

    /// <summary>Whether <paramref name="args"/> is 'index embed' without pause or resume: it runs the service as a job and follows it.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>True for the running form.</returns>
    internal static bool Runs(string[] args) => args is ["index", "embed"] or ["index", "embed", not ("pause" or "resume"), ..];

    /// <summary>The arguments after 'services' for what follows 'index embed'.</summary>
    /// <param name="rest">The arguments after 'index embed'.</param>
    /// <returns>The arguments after 'services' ('run index-embed …'), or SND-INPUT-001 naming the usage.</returns>
    internal static Result<string[]> Services(string[] rest)
    {
        ArgumentNullException.ThrowIfNull(rest);
        if (rest is ["pause" or "resume", .. var flags])
        {
            return (string[])[rest[0], ServiceName, .. flags];
        }

        return rest is [var first, ..] && !first.StartsWith("--", StringComparison.Ordinal)
            ? SindriErrors.InvalidInput.With($"'index embed {string.Join(' ', rest)}' isn't an index embed command: {Usage}")
            : (string[])["run", ServiceName, .. rest];
    }

    private const string ServiceName = "index-embed";
}
