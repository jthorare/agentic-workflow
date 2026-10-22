using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The contract every <see cref="IDefaultBranch"/> honours: the default branch's name without a remote prefix; an error for a checkout that has none.</summary>
public abstract class DefaultBranchContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The adapter over a checkout whose default branch is 'trunk', and one whose default branch is 'release/1'.</summary>
    /// <returns>The adapter and the two checkouts.</returns>
    protected abstract ValueTask<(IDefaultBranch Port, string Trunk, string Slashed)> Create();

    /// <summary>The adapter and a checkout that has no default branch (no remote HEAD, or no such checkout).</summary>
    /// <returns>The adapter and the checkout.</returns>
    protected abstract ValueTask<(IDefaultBranch Port, string Checkout)> CreateWithoutDefault();

    /// <summary>The default branch is named as the repo does.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_default_branch_is_named_without_a_remote_prefix()
    {
        var (port, trunk, _) = await Create();

        var found = await port.Of(trunk, Token);

        Expect.True(found.IsSuccess);
        Expect.Equal("trunk", found.Value);
    }

    /// <summary>A branch name with a slash keeps it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_branch_with_a_slash_keeps_it()
    {
        var (port, _, slashed) = await Create();

        var found = await port.Of(slashed, Token);

        Expect.True(found.IsSuccess);
        Expect.Equal("release/1", found.Value);
    }

    /// <summary>A checkout that can't tell is an error, never a guess.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_checkout_with_no_default_branch_is_an_error()
    {
        var (port, checkout) = await CreateWithoutDefault();

        var found = await port.Of(checkout, Token);

        Expect.True(found.IsError);
    }
}
