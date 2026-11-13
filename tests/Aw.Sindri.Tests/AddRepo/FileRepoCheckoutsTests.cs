using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure.Setup;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>
/// The checkout check the context reads ask before any git read (#3371), on real folders: '.git' as a folder or a file is a checkout, a plain folder
/// is not, even inside a checkout, and a missing folder is told apart from both (<see cref="RepoCheckoutsContract"/>).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class FileRepoCheckoutsTests : RepoCheckoutsContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override IRepoCheckouts Create() => new FileRepoCheckouts();

    protected override string Clone()
    {
        var clone = Folder("clone");
        _ = Directory.CreateDirectory(Path.Combine(clone, ".git"));
        return clone;
    }

    protected override string Worktree()
    {
        var worktree = Folder("worktree");
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: /elsewhere/.git/worktrees/worktree\n");
        return worktree;
    }

    protected override string PlainFolder() => Folder("notes");

    protected override string PlainFolderInside(string clone) => Directory.CreateDirectory(Path.Combine(clone, "notes")).FullName;

    protected override string MissingFolder() => Path.Combine(_home.Path, "gone");

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_home.Path, name)).FullName;
}
