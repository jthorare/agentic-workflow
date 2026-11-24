using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>The git reads the drift checks make in the toolkit checkout: each one bounded by a timeout, and a failure is no answer, never an error.</summary>
/// <param name="runner">Runs git.</param>
internal sealed class ToolkitGit(ICommandRunner runner)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <summary>git's whole stdout when it exits 0 in time.</summary>
    /// <param name="toolkit">The checkout.</param>
    /// <param name="args">The git arguments after -C &lt;toolkit&gt;.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The output; null when git failed, timed out or printed nothing.</returns>
    internal async ValueTask<string?> Text(string toolkit, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var run = await Run(toolkit, args, cancellationToken).ConfigureAwait(false);
        return run is { ExitCode: 0, Stdout: var stdout } && stdout.Trim().Length > 0 ? stdout : null;
    }

    /// <summary>The first line git prints when it exits 0 in time.</summary>
    /// <param name="toolkit">The checkout.</param>
    /// <param name="args">The git arguments after -C &lt;toolkit&gt;.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The line, trimmed; null when git failed, timed out or printed nothing.</returns>
    internal async ValueTask<string?> Line(string toolkit, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var text = await Text(toolkit, args, cancellationToken).ConfigureAwait(false);
        return text?.Trim().Split('\n')[0].Trim();
    }

    /// <summary>git's exit code.</summary>
    /// <param name="toolkit">The checkout.</param>
    /// <param name="args">The git arguments after -C &lt;toolkit&gt;.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The code; null when git could not run or timed out.</returns>
    internal async ValueTask<int?> Exit(string toolkit, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var run = await Run(toolkit, args, cancellationToken).ConfigureAwait(false);
        return run?.ExitCode;
    }

    private async ValueTask<CommandResult?> Run(string toolkit, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var run = await runner.Run(new CommandRequest(["git", "-C", toolkit, .. args], _timeout), cancellationToken).ConfigureAwait(false);
        return run.IsSuccess && !run.Value.TimedOut ? run.Value : null;
    }
}
