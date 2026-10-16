using Aw.Sindri.Core.Workflow.Engine.Effects;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IClaimRefs"/> honours (engine spec §12 'effect.claim.ref'): a ref is created once, and
/// marking it again reports that it existed; refs are independent.
/// </summary>
public abstract class ClaimRefsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The adapter and a repo with one commit it marks in.</summary>
    /// <returns>The adapter and the repo's path.</returns>
    protected abstract ValueTask<(IClaimRefs Refs, string Repository)> Create();

    /// <summary>The first mark creates the ref; the second finds it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_ref_is_created_once()
    {
        var (refs, repository) = await Create();

        var first = await refs.Mark(repository, "refs/sindri/dev/T-3", Token);
        var again = await refs.Mark(repository, "refs/sindri/dev/T-3", Token);

        Expect.True(first.IsSuccess && first.Value);
        Expect.True(again.IsSuccess && !again.Value);
    }

    /// <summary>Another item's ref is its own.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Refs_are_independent()
    {
        var (refs, repository) = await Create();
        _ = await refs.Mark(repository, "refs/sindri/dev/T-3", Token);

        var other = await refs.Mark(repository, "refs/sindri/dev/T-4", Token);

        Expect.True(other.IsSuccess && other.Value);
    }
}
