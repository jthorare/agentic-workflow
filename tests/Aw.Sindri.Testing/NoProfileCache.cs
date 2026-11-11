using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IProfileCache"/> that keeps nothing, for tests that build a profile writer and read the file straight back.</summary>
public sealed class NoProfileCache : IProfileCache
{
    /// <summary>The one instance.</summary>
    public static NoProfileCache Instance { get; } = new();

    /// <inheritdoc/>
    public void Invalidate()
    {
    }
}
