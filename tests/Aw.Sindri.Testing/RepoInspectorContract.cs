using Aw.Sindri.Core.Setup;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IRepoInspector"/> honours: a checkout inspects to itself with its git facts, a folder that is not a checkout is SND-SETUP-001, and so is a path that does not exist.</summary>
public abstract class RepoInspectorContract
{
    /// <summary>An inspector and the path of a folder that is a git checkout on branch main with at least one commit, and a folder that is not a checkout.</summary>
    /// <returns>The inspector, the checkout and the folder outside git.</returns>
    protected abstract ValueTask<(IRepoInspector Inspector, string Checkout, string NotACheckout)> Create();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A checkout resolves to itself and git's facts come with it.</summary>
    [Fact]
    public async Task A_checkout_inspects_to_itself_with_its_branch_and_head()
    {
        var (inspector, checkout, _) = await Create();

        var inspected = await inspector.Inspect(checkout, Token);

        Expect.True(inspected.IsSuccess);
        Expect.Equal(checkout, inspected.Value.Checkout);
        Expect.Equal("main", inspected.Value.Git.DefaultBranch);
        Expect.True(inspected.Value.Git.Head is { Length: > 0 });
    }

    /// <summary>A folder outside git is SND-SETUP-001 with the fix in the sentence.</summary>
    [Fact]
    public async Task A_folder_outside_git_is_refused_with_the_fix()
    {
        var (inspector, _, outside) = await Create();

        var inspected = await inspector.Inspect(outside, Token);

        Expect.Equal("SND-SETUP-001", inspected.Error.Code);
    }

    /// <summary>A path that does not exist is SND-SETUP-001.</summary>
    [Fact]
    public async Task A_missing_folder_is_refused()
    {
        var (inspector, _, outside) = await Create();

        var inspected = await inspector.Inspect(Path.Combine(outside, "nothing-here"), Token);

        Expect.Equal("SND-SETUP-001", inspected.Error.Code);
    }

    /// <summary>The facts of a checkout are the same facts.</summary>
    [Fact]
    public async Task The_facts_of_a_checkout_are_its_branch_and_head()
    {
        var (inspector, checkout, _) = await Create();

        var facts = await inspector.Facts(checkout, Token);

        Expect.Equal("main", facts.Value.DefaultBranch);
    }

    /// <summary>The facts of a folder that is not a checkout are SND-SETUP-001.</summary>
    [Fact]
    public async Task The_facts_of_a_folder_outside_git_are_refused()
    {
        var (inspector, _, outside) = await Create();

        var facts = await inspector.Facts(outside, Token);

        Expect.Equal("SND-SETUP-001", facts.Error.Code);
    }
}
