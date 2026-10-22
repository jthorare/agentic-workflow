using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory IRecipeBuild: answers the trees the test gave (null: no build stamp).</summary>
/// <param name="trees">The answer.</param>
public sealed class FakeRecipeBuild(RecipeTrees? trees) : IRecipeBuild
{
    /// <inheritdoc/>
    public ValueTask<RecipeTrees?> Read(CancellationToken cancellationToken) => ValueTask.FromResult(trees);
}
