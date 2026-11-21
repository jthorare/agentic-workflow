using Aw.Sindri.Core;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Lanes;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Lanes;

/// <summary>
/// <see cref="ILaneSource"/> over the 'aw-meta' binary on PATH, through <see cref="IToolRunner"/> (Home spec §3.3). It runs only 'status --json', 'lanes-context --json' and 'usage --json',
/// each with a timeout. Exit 0 and exit 1 both carry valid JSON ('lanes-context' and 'usage' exit 1 when something needs attention); a missing binary, a timeout, exit 2 or more, or empty output is SND-LANES-001.
/// </summary>
/// <param name="runner">Runs the tool.</param>
/// <param name="timeout">How long one command may run.</param>
internal sealed class AwMetaLaneSource(IToolRunner runner, TimeSpan timeout) : ILaneSource
{
    internal const string Tool = "aw-meta";

    public ValueTask<Result<string>> Status(CancellationToken cancellationToken) => Read("status", cancellationToken);

    public ValueTask<Result<string>> Context(CancellationToken cancellationToken) => Read("lanes-context", cancellationToken);

    public ValueTask<Result<string>> Usage(CancellationToken cancellationToken) => Read("usage", cancellationToken);

    private ValueTask<Result<string>> Read(string command, CancellationToken cancellationToken)
    {
        var run = runner.Run(Tool, [command, "--json"], timeout, options: null, cancellationToken);
        Result<string> answer = run switch
        {
            { Outcome: ToolOutcome.Missing } => SindriErrors.LanesUnavailable.With("aw-meta is not on PATH"),
            { Outcome: ToolOutcome.TimedOut } => SindriErrors.LanesUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"aw-meta {command} did not finish in {timeout.TotalSeconds:0} s")),
            { Outcome: ToolOutcome.Cancelled } => SindriErrors.LanesUnavailable.With($"aw-meta {command} was cancelled"),
            { ExitCode: > 1 } => SindriErrors.LanesUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"aw-meta {command} exited {run.ExitCode}")),
            { Output: var output } when string.IsNullOrWhiteSpace(output) => SindriErrors.LanesUnavailable.With($"aw-meta {command} printed nothing"),
            _ => run.Output,
        };
        return ValueTask.FromResult(answer);
    }
}
