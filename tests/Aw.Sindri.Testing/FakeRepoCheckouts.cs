using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>
/// The checkout check in memory (#3371, #3408): each path named here has its kind, and every other path reads as <paramref name="otherwise"/>.
/// The default reads every repo as a checkout, so a test that doesn't care about plain folders keeps every repo in the map. Kept honest by
/// <see cref="RepoCheckoutsContract"/>.
/// </summary>
/// <param name="otherwise">The kind of a path not named here.</param>
public sealed class FakeRepoCheckouts(RepoCheckout otherwise = RepoCheckout.Checkout) : IRepoCheckouts
{
    private readonly Dictionary<string, RepoCheckout> _paths = new(StringComparer.Ordinal);

    /// <summary>Records what <paramref name="path"/> holds.</summary>
    /// <param name="path">A repo path, as the profile writes it.</param>
    /// <param name="kind">What the path holds.</param>
    /// <returns>This fake.</returns>
    public FakeRepoCheckouts At(string path, RepoCheckout kind)
    {
        _paths[path] = kind;
        return this;
    }

    /// <inheritdoc/>
    public RepoCheckout Of(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _paths.TryGetValue(path, out var kind) ? kind : otherwise;
    }
}
