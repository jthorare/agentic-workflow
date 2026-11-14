using Aw.Sindri.Core.Setup;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IRepoCheckouts"/> honours (#3371, #3408), run against the fake and the file adapter.</summary>
public abstract class RepoCheckoutsContract
{
    /// <summary>The checkout check under test.</summary>
    /// <returns>The check.</returns>
    protected abstract IRepoCheckouts Create();

    /// <summary>A clone: a folder with a '.git' folder.</summary>
    /// <returns>Its path.</returns>
    protected abstract string Clone();

    /// <summary>A linked worktree: a folder with a '.git' file.</summary>
    /// <returns>Its path.</returns>
    protected abstract string Worktree();

    /// <summary>A folder with no '.git', outside any checkout.</summary>
    /// <returns>Its path.</returns>
    protected abstract string PlainFolder();

    /// <summary>A folder with no '.git' of its own, inside the clone <paramref name="clone"/>.</summary>
    /// <param name="clone">A path <see cref="Clone"/> returned.</param>
    /// <returns>Its path.</returns>
    protected abstract string PlainFolderInside(string clone);

    /// <summary>A path where no folder is.</summary>
    /// <returns>The path.</returns>
    protected abstract string MissingFolder();

    /// <summary>A clone and a linked worktree are both checkouts.</summary>
    [Fact]
    public void A_clone_and_a_linked_worktree_are_checkouts()
    {
        var checkouts = Create();

        Expect.Equal(RepoCheckout.Checkout, checkouts.Of(Clone()));
        Expect.Equal(RepoCheckout.Checkout, checkouts.Of(Worktree()));
    }

    /// <summary>A plain folder is not a checkout, even inside one, where git would read the parent's commits.</summary>
    [Fact]
    public void A_plain_folder_is_not_a_checkout_even_inside_one()
    {
        var checkouts = Create();

        Expect.Equal(RepoCheckout.PlainFolder, checkouts.Of(PlainFolder()));
        Expect.Equal(RepoCheckout.PlainFolder, checkouts.Of(PlainFolderInside(Clone())));
    }

    /// <summary>A missing folder is told apart from a plain folder, so its reads keep failing loudly.</summary>
    [Fact]
    public void A_missing_folder_is_missing_not_a_plain_folder() => Expect.Equal(RepoCheckout.Missing, Create().Of(MissingFolder()));
}
