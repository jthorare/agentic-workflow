using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILogFiles: the one fake, kept honest by LogFilesContract.</summary>
public sealed class InMemoryLogFiles : ILogFiles
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _claimed = new(StringComparer.Ordinal);

    /// <summary>Puts a file in place.</summary>
    /// <param name="path">The path.</param>
    /// <param name="text">Its text.</param>
    public void Put(string path, string text) => _files[path] = text;

    /// <inheritdoc />
    public ValueTask<Result<int>> LatestRun(string gateDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gateDirectory);
        var prefix = gateDirectory.TrimEnd('/') + "/";
        return Done.Of(_files.Keys.Concat(_claimed)
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(path => path[prefix.Length..].Split('/')[0])
            .Where(name => name.Length > 0 && name.All(char.IsAsciiDigit) && int.TryParse(name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _))
            .Select(name => int.Parse(name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max());
    }

    /// <inheritdoc />
    public async ValueTask<Result<int>> ClaimRun(string gateDirectory, int recorded, CancellationToken cancellationToken)
    {
        var latest = await LatestRun(gateDirectory, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest;
        }

        var run = Math.Max(recorded, latest.Value) + 1;
        _ = _claimed.Add(gateDirectory.TrimEnd('/') + "/" + run.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/.claimed");
        return run;
    }

    /// <inheritdoc />
    public ValueTask<Result<string>> Tail(string path, int maxCharacters, CancellationToken cancellationToken) =>
        _files.TryGetValue(path, out var text)
            ? Done.Of(text.Length <= maxCharacters ? text : text[^maxCharacters..])
            : Done.Failed<string>(MetaErrors.LogUnreadable.With("no such file: " + path));
}
