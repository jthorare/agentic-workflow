using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure.Setup;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The checkout check the context reads ask before any git read (#3371): '.git' as a folder or a file is a checkout, a plain folder is not, even inside a checkout, and a missing folder is told apart from both.</summary>
[Lane(TestLane.Integration)]
public sealed class FileRepoCheckoutsTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FileRepoCheckouts _checkouts = new();

    public void Dispose() => _home.Dispose();

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_home.Path, name)).FullName;

    [Fact]
    public void A_folder_with_a_git_folder_is_a_checkout()
    {
        var clone = Folder("clone");
        _ = Directory.CreateDirectory(Path.Combine(clone, ".git"));

        Assert.Equal(RepoCheckout.Checkout, _checkouts.Of(clone));
    }

    [Fact]
    public void A_folder_with_a_git_file_is_a_checkout_as_a_linked_worktree_is()
    {
        var worktree = Folder("worktree");
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: /elsewhere/.git/worktrees/worktree\n");

        Assert.Equal(RepoCheckout.Checkout, _checkouts.Of(worktree));
    }

    [Fact]
    public void A_plain_folder_is_not_a_checkout() => Assert.Equal(RepoCheckout.PlainFolder, _checkouts.Of(Folder("notes")));

    [Fact]
    public void A_plain_folder_inside_a_checkout_is_not_a_checkout_of_its_own()
    {
        var clone = Folder("clone");
        _ = Directory.CreateDirectory(Path.Combine(clone, ".git"));

        Assert.Equal(RepoCheckout.PlainFolder, _checkouts.Of(Folder(Path.Combine("clone", "notes"))));
    }

    [Fact]
    public void A_missing_folder_is_missing_not_a_plain_folder() => Assert.Equal(RepoCheckout.Missing, _checkouts.Of(Path.Combine(_home.Path, "gone")));
}
