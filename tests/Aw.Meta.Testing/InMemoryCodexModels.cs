using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ICodexModels: nothing listed (no cache file) until a test sets the slugs.</summary>
public sealed class InMemoryCodexModels : ICodexModels
{
    private IReadOnlySet<string>? _slugs;

    /// <summary>Sets the slugs the cache lists.</summary>
    /// <param name="slugs">The slugs.</param>
    public void List(params string[] slugs) => _slugs = new HashSet<string>(slugs, StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlySet<string>?>> Known(CancellationToken cancellationToken) => Done.Of(_slugs);
}
