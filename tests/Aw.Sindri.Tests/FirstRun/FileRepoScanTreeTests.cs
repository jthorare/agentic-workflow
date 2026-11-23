using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Setup;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.FirstRun;

/// <summary>The scan's file system on real folders (first-run spec §3.3, <see cref="RepoScanTreeContract"/>): links left out, '.git' told apart, '.git/worktrees' counted, the origin read with git from '.git/config'.</summary>
[Lane(TestLane.Integration)]
public sealed class FileRepoScanTreeTests : RepoScanTreeContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override IRepoScanTree Create() => new FileRepoScanTree(HomeFolder());

    protected override string HomeFolder() => RealPath.Of(_home.Path) ?? _home.Path;   // macOS's /var is /private/var

    protected override string Folder(string relative) => Directory.CreateDirectory(Path.Combine(HomeFolder(), relative)).FullName;

    protected override string Checkout(string relative, string? origin)
    {
        var checkout = Folder(relative);
        var git = Directory.CreateDirectory(Path.Combine(checkout, ".git")).FullName;
        File.WriteAllText(Path.Combine(git, "config"), origin is null ? "[core]\n\tbare = false\n" : $"[core]\n\tbare = false\n[remote \"origin\"]\n\turl = {origin}\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n");
        return checkout;
    }

    protected override string Worktree(string relative, string main)
    {
        var worktree = Folder(relative);
        var name = Path.GetFileName(worktree);
        var entry = Directory.CreateDirectory(Path.Combine(main, ".git", "worktrees", name)).FullName;
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {entry}\n");
        return worktree;
    }

    protected override string Link(string relative, string target)
    {
        var link = Path.Combine(HomeFolder(), relative);
        _ = Directory.CreateSymbolicLink(link, target);
        return link;
    }
}
