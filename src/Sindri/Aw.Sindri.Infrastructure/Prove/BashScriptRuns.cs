using Aw.Sindri.Core.Prove;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Prove;

/// <summary>
/// <see cref="IScriptRuns"/> over 'bash': the script file runs with the checkout as its working directory, through <see cref="IToolRunner"/> (an argument list, never a shell string built
/// from the paths). Exit 126 and 127 are bash saying it could not run the file, which is "could not run", not a failing proof.
/// </summary>
/// <param name="runner">Runs bash.</param>
internal sealed class BashScriptRuns(IToolRunner runner) : IScriptRuns
{
    private const int NotExecutable = 126;
    private const int NotFound = 127;

    /// <inheritdoc/>
    public ValueTask<int?> Run(string directory, string script, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // The directory and the script are positional parameters of the one-line program, so neither is ever parsed as shell text.
        var run = runner.Run("bash", ["-c", "cd -- \"$1\" && exec bash -- \"$2\"", "bash", directory, script], timeout, options: null, cancellationToken);
        return ValueTask.FromResult<int?>(run.Outcome == ToolOutcome.Exited && run.ExitCode is not (NotExecutable or NotFound) ? run.ExitCode : null);
    }
}
