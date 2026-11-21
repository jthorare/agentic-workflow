using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Registered in DI by the fix commit of #3733; the red stub has no caller yet.")]
internal sealed class AwMetaLaneSource(IToolRunner runner, TimeSpan timeout) : ILaneSource
{
    internal const string Tool = "aw-meta";

    public ValueTask<Result<string>> Status(CancellationToken cancellationToken) => Read("status", cancellationToken);

    public ValueTask<Result<string>> Context(CancellationToken cancellationToken) => Read("lanes-context", cancellationToken);

    public ValueTask<Result<string>> Usage(CancellationToken cancellationToken) => Read("usage", cancellationToken);

    private ValueTask<Result<string>> Read(string command, CancellationToken cancellationToken)
    {
        _ = (runner, timeout, command, cancellationToken);   // red stub
        throw new NotSupportedException();
    }
}
