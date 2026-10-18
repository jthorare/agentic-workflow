namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Finds an agent CLI: an absolute path by its file, a name through the login shell (the name is a positional argument, never interpolated).</summary>
internal static class BinResolver
{
    internal static async ValueTask<string?> Resolve(string bin, CancellationToken cancellationToken)
    {
        if (bin.StartsWith('/'))
        {
            return File.Exists(bin) ? bin : null;
        }

        var found = await ViewerProcess.Output("bash", ["-lc", "command -v -- \"$1\"", "_", bin], cancellationToken).ConfigureAwait(false);
        return LastLine(found);
    }

    /// <summary>The last non-empty line of a login shell's output: a profile may print before `command -v` does.</summary>
    /// <param name="output">The shell's stdout, or null.</param>
    /// <returns>The line, or null when there is none.</returns>
    internal static string? LastLine(string? output) =>
        output?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
}
