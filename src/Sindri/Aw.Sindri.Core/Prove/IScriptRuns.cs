namespace Aw.Sindri.Core.Prove;

/// <summary>Runs a recorded proof script in a checkout (port; the before/after Step runs the same script at the phase's base and at its head, line spec §7.3).</summary>
public interface IScriptRuns
{
    /// <summary>Runs the script with its working directory set to <paramref name="directory"/>, killing it past <paramref name="timeout"/>.</summary>
    /// <param name="directory">The checkout the script runs in, an absolute path.</param>
    /// <param name="script">The script, an absolute path to the file.</param>
    /// <param name="timeout">How long it may run.</param>
    /// <param name="cancellationToken">Cancels the run: the script and its children are killed.</param>
    /// <returns>The script's exit code (0 passed, anything else failed); null when it could not run (no shell, the file is missing or not executable by the shell, a timeout, a cancel).</returns>
    public ValueTask<int?> Run(string directory, string script, TimeSpan timeout, CancellationToken cancellationToken);
}
