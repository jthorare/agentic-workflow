using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Testing.Worktrees;

/// <summary>An <see cref="ITrackedFiles"/> in memory: the paths a test gave for a checkout, an error for any other.</summary>
public sealed class FakeTrackedFiles : ITrackedFiles
{
    private readonly Dictionary<string, IReadOnlyList<string>> _files = new(StringComparer.Ordinal);

    /// <summary>Holds the tracked paths of a checkout.</summary>
    /// <param name="checkout">The checkout.</param>
    /// <param name="paths">Its tracked paths.</param>
    /// <returns>The same instance.</returns>
    public FakeTrackedFiles With(string checkout, params string[] paths)
    {
        _files[checkout] = [.. paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> Paths(string checkout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_files.TryGetValue(checkout, out var paths) ? Results.Success(paths) : SindriErrors.InvalidInput.With($"{checkout} is not a checkout"));
    }
}
