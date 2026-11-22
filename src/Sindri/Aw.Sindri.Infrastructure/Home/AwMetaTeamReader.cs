using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Home;

/// <summary>
/// <see cref="IMetaTeamReader"/> over the aw-meta program (AW_SINDRI_AW_META, else 'aw-meta' on the PATH), run through <see cref="IToolRunner"/> with an argument list and no shell:
/// 'lanes-context --json', 'status --json', 'usage --json' and 'team-list --json'. 'lanes-context' and 'usage' exit 1 when something needs attention and still print JSON, so exit 0
/// and 1 both parse stdout; a missing program, a timeout, any other exit or JSON that does not parse makes the whole read unavailable (home spec §8, 'lanes-unavailable').
/// 'team-list' is optional: without it the read has no repository directory.
/// </summary>
/// <param name="runner">Runs aw-meta.</param>
/// <param name="environment">Reads AW_SINDRI_AW_META.</param>
internal sealed class AwMetaTeamReader(IToolRunner runner, Func<string, string?> environment) : IMetaTeamReader
{
    /// <summary>The environment variable naming the aw-meta program.</summary>
    internal const string ProgramVariable = "AW_SINDRI_AW_META";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    public ValueTask<MetaTeamRead> Read(CancellationToken cancellationToken)
    {
        var program = environment(ProgramVariable) is { Length: > 0 } set ? set : "aw-meta";
        var context = Run(program, "lanes-context", cancellationToken);
        var status = context.Problem is null ? Run(program, "status", cancellationToken) : context;
        var usage = status.Problem is null ? Run(program, "usage", cancellationToken) : status;
        if ((context.Problem ?? status.Problem ?? usage.Problem) is { } problem)
        {
            return ValueTask.FromResult(MetaTeamRead.NotAvailable(problem));
        }

        var teams = Run(program, "team-list", cancellationToken);
        try
        {
            return ValueTask.FromResult(AwMetaJson.Parse(context.Output, status.Output, usage.Output, teams.Problem is null ? teams.Output : null));
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)   // boundary: aw-meta's JSON is outside text; a shape this build doesn't know is 'unavailable', never a crash
        {
            return ValueTask.FromResult(MetaTeamRead.NotAvailable($"aw-meta printed JSON this build can't read ({e.GetType().Name})"));
        }
    }

    private (string Output, string? Problem) Run(string program, string command, CancellationToken cancellationToken)
    {
        var run = runner.Run(program, [command, "--json"], _timeout, options: null, cancellationToken);
        return run.Outcome switch
        {
            ToolOutcome.Missing => ("", $"{program} is not on the PATH (set {ProgramVariable} or install aw-meta)"),
            ToolOutcome.TimedOut => ("", $"aw-meta {command} ran past {_timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s"),
            ToolOutcome.Exited when run.ExitCode is 0 or 1 => (run.Output, null),
            ToolOutcome.Exited => ("", string.Create(CultureInfo.InvariantCulture, $"aw-meta {command} exited {run.ExitCode}")),
            _ => ("", $"aw-meta {command} was cancelled"),
        };
    }
}
