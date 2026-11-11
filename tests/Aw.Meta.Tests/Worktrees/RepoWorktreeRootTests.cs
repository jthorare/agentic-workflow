using Aw.Meta.Core;
using Aw.Meta.Core.Worktrees;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>The root the sweep and the watch add to the default roots: the resolved setting, or null on any failure (ideas' review of #2848).</summary>
public sealed class RepoWorktreeRootTests
{
    private const string Repo = "/work";
    private const string Home = "/home/u";
    private readonly InMemoryWorktreeFiles _files = new();

    [Fact, Trait("Lane", "unit")]
    public async Task A_root_inside_the_repo_is_left_out()
    {
        _files.ConfigureRoot(Repo, "trees");

        Assert.Null(await RepoWorktreeRoot.ResolveOrNull(_files, Repo, Home, CancellationToken.None));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_root_that_cannot_be_read_is_left_out()
    {
        _files.ConfigureRoot(Repo, "/data/trees");
        _files.Failure = MetaErrors.HostUnreadable.With("denied");

        Assert.Null(await RepoWorktreeRoot.ResolveOrNull(_files, Repo, Home, CancellationToken.None));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_accepted_root_is_returned_resolved()
    {
        _files.ConfigureRoot(Repo, "../trees/work");

        Assert.Equal("/trees/work", await RepoWorktreeRoot.ResolveOrNull(_files, Repo, Home, CancellationToken.None));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_configured_gives_the_default_root() =>
        Assert.Equal("/home/u/repos/aw-wt/work", await RepoWorktreeRoot.ResolveOrNull(_files, Repo, Home, CancellationToken.None));
}
