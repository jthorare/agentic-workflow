using System.Globalization;
using Aw.Evidence.Core.Desktop;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IScratchDirectories that keeps its directories as names: /tmp/&lt;prefix&gt;&lt;n&gt;.</summary>
public sealed class FakeScratchDirectories : IScratchDirectories
{
    private readonly HashSet<string> _live = new(StringComparer.Ordinal);
    private int _next;

    /// <summary>Every directory made, in order.</summary>
    public IList<string> Made { get; } = [];

    /// <summary>The directories made and not removed.</summary>
    public IReadOnlyCollection<string> Live => _live;

    /// <summary>Whether <paramref name="path"/> was made and not removed.</summary>
    /// <param name="path">The directory.</param>
    /// <returns>Whether it exists.</returns>
    public bool Exists(string path) => _live.Contains(path);

    /// <inheritdoc/>
    public Result<string> Create(string prefix)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"/tmp/{prefix}{++_next}");
        _ = _live.Add(path);
        Made.Add(path);
        return path;
    }

    /// <inheritdoc/>
    public void Remove(string path) => _ = _live.Remove(path);
}
