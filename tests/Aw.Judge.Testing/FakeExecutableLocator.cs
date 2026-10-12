using Aw.Judge.Core.Ports;

namespace Aw.Judge.Testing;

/// <summary>The IExecutableLocator fake: the given names are installed; it passes ExecutableLocatorContract.</summary>
/// <param name="installed">The installed binary names.</param>
public sealed class FakeExecutableLocator(params string[] installed) : IExecutableLocator
{
    private readonly HashSet<string> _installed = new(installed, StringComparer.Ordinal);
    private readonly List<string> _asked = [];

    /// <summary>Every name asked about, in order.</summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <inheritdoc />
    public bool IsOnPath(string name)
    {
        _asked.Add(name);
        return _installed.Contains(name);
    }
}
