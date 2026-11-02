using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri jobs …' parsed: the verb, the job it names, and the filters and flags.</summary>
/// <param name="Verb">'list', 'show', 'cancel' or 'resume'.</param>
/// <param name="Job">The job id text for show, cancel and resume, or null.</param>
/// <param name="State">--state, or null.</param>
/// <param name="Kind">--kind, or null.</param>
/// <param name="Json">--json.</param>
/// <param name="NoStart">--no-start.</param>
internal sealed record JobCommandLine(string Verb, string? Job, string? State, string? Kind, bool Json, bool NoStart)
{
    internal const string Usage = "jobs [--state <s>] [--kind <k>] [--json] [--no-start] | jobs show <jobId> [--json] [--no-start] | jobs cancel <jobId> [--no-start] | jobs resume <jobId> [--no-start]";

    /// <summary>Reads the arguments after 'jobs'.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The command line, or SND-CLI-003 naming the usage.</returns>
    internal static Result<JobCommandLine> Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var verb = args is ["show" or "cancel" or "resume", ..] ? args[0] : "list";
        var rest = (verb == "list" ? args : args[1..]).ToList();
        var json = rest.Remove("--json");
        var noStart = rest.Remove("--no-start");
        var state = verb == "list" ? Take(rest, "--state") : null;
        var kind = verb == "list" ? Take(rest, "--kind") : null;
        var refused = rest.Count != (verb == "list" ? 0 : 1) || rest.Exists(static w => w.StartsWith("--", StringComparison.Ordinal));
        return refused ? Refused(args) : new JobCommandLine(verb, rest.FirstOrDefault(), state, kind, json, noStart);
    }

    /// <summary>Removes '<paramref name="flag"/> value' from <paramref name="words"/> and returns the value; null when the flag isn't there. A flag with no value stays, so the line is refused.</summary>
    private static string? Take(List<string> words, string flag)
    {
        var at = words.IndexOf(flag);
        if (at < 0 || at + 1 >= words.Count)
        {
            return null;
        }

        var value = words[at + 1];
        words.RemoveRange(at, 2);
        return value;
    }

    private static Error Refused(string[] args) => SindriErrors.InvalidInput.With($"'{string.Join(' ', args)}' isn't a jobs command: {Usage}");
}
