using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Checks;

/// <summary>The git adapter of the branch changes against real temp repositories.</summary>
[Lane(TestLane.Integration)]
public sealed class GitBranchChangesContractTests : BranchChangesContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    protected override async ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> Linear()
    {
        var repo = await Base();
        var baseSha = await repo.Git("rev-parse", "HEAD");
        await Write(repo, "src/b.cs", "x1\nx2\n");
        await Commit(repo, "feat: add b");
        await File.AppendAllTextAsync(Path.Combine(repo.Path, "src/a.cs"), "two\n", TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(repo.Path, "tests/t.test.ts"));
        await Commit(repo, "fix: tweak a");
        return (new GitBranchChanges(), repo.Path, baseSha, await repo.Git("rev-parse", "HEAD"));
    }

    protected override async ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> WithMerge()
    {
        var repo = await Base();
        var baseSha = await repo.Git("rev-parse", "HEAD");
        _ = await repo.Git("checkout", "-q", "-b", "side");
        await Write(repo, "src/s.cs", "s\n");
        await Commit(repo, "feat: side");
        _ = await repo.Git("checkout", "-q", "main");
        await Write(repo, "src/m.cs", "m\n");
        await Commit(repo, "feat: main");
        _ = await repo.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "merge", "-q", "--no-ff", "-m", "Merge side", "side");
        return (new GitBranchChanges(), repo.Path, baseSha, await repo.Git("rev-parse", "HEAD"));
    }

    protected override async ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> Unknown()
    {
        var repo = await Base();
        return (new GitBranchChanges(), repo.Path, "0123456789012345678901234567890123456789", await repo.Git("rev-parse", "HEAD"));
    }

    private async Task<TempGitRepo> Base()
    {
        var repo = await TempGitRepo.Create(commits: 0);
        _repos.Add(repo);
        await Write(repo, "src/a.cs", "one\n");
        await Write(repo, "tests/t.test.ts", "test\n");
        await Commit(repo, "chore: base");
        return repo;
    }

    private static async Task Write(TempGitRepo repo, string path, string text)
    {
        var full = Path.Combine(repo.Path, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);   // why: a combined path has a directory
        await File.WriteAllTextAsync(full, text, TestContext.Current.CancellationToken);
    }

    private static async Task Commit(TempGitRepo repo, string subject)
    {
        _ = await repo.Git("add", "-A");
        _ = await repo.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "-m", subject);
    }
}
