using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing.Prepare;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Prepare;

/// <summary>The git adapter of the prepare source against a real temp repository.</summary>
[Lane(TestLane.Integration)]
public sealed class GitPrepareSourceContractTests : PrepareSourceContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    protected override async ValueTask<(IPrepareSource Port, string Repo, string First, string Second)> Scenario()
    {
        var repo = await TempGitRepo.Create(commits: 0);
        _repos.Add(repo);
        var first = await Commit(repo, "LOCK-A");
        var second = await Commit(repo, "LOCK-B");
        return (new GitPrepareSource(), repo.Path, first, second);
    }

    private static async Task<string> Commit(TempGitRepo repo, string text)
    {
        var path = Path.Combine(repo.Path, Lockfile);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
        _ = await repo.Git("add", Lockfile);
        _ = await repo.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "-m", "lock " + text);
        return await repo.Git("rev-parse", "HEAD");
    }
}
