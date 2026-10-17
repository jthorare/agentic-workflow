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
        return found is { Length: > 0 } ? found : null;
    }
}
