namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Runs one of the macOS tools (ps, lsof) and returns its standard output.</summary>
internal interface IMacTools
{
    /// <summary>Runs <paramref name="tool"/> to its end within its budget.</summary>
    /// <param name="tool">The program's absolute path.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Standard output, or null when the tool is missing, timed out, was cancelled or failed without output (lsof exits 1 when it could not read some of what it lists: output with exit 1 is an answer).</returns>
    public string? Run(string tool, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
