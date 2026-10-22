using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>The git adapter of the default branch against real temp repos: origin/HEAD pointing at a remote-tracking branch.</summary>
[Lane(TestLane.Integration)]
public sealed class GitDefaultBranchContractTests : DefaultBranchContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    protected override async ValueTask<(IDefaultBranch Port, string Trunk, string Slashed)> Create()
    {
        var trunk = await RepoWithOriginHead("trunk");
        var slashed = await RepoWithOriginHead("release/1");
        return (new GitDefaultBranch(), trunk.Path, slashed.Path);
    }

    protected override async ValueTask<(IDefaultBranch Port, string Checkout)> CreateWithoutDefault()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return (new GitDefaultBranch(), repo.Path);
    }

    private async Task<TempGitRepo> RepoWithOriginHead(string branch)
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        _ = await repo.Git("update-ref", $"refs/remotes/origin/{branch}", "HEAD");
        _ = await repo.Git("symbolic-ref", "refs/remotes/origin/HEAD", $"refs/remotes/origin/{branch}");
        return repo;
    }
}
