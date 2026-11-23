using Aw.Sindri.Core.Setup;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IRepoScanTree"/> honours (first-run spec §3.3), run against the fake and the file adapter. Every fixture lies under the tree's home folder.</summary>
public abstract class RepoScanTreeContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The tree under test, made after the fixtures.</summary>
    /// <returns>The tree.</returns>
    protected abstract IRepoScanTree Create();

    /// <summary>A plain folder at <paramref name="relative"/> under the home folder.</summary>
    /// <param name="relative">The path under the home folder ('a/b').</param>
    /// <returns>Its real path.</returns>
    protected abstract string Folder(string relative);

    /// <summary>A main checkout ('.git' folder) whose origin is <paramref name="origin"/>.</summary>
    /// <param name="relative">The path under the home folder.</param>
    /// <param name="origin">The origin URL, or null for none.</param>
    /// <returns>Its real path.</returns>
    protected abstract string Checkout(string relative, string? origin);

    /// <summary>A linked worktree ('.git' file) of the main checkout <paramref name="main"/>.</summary>
    /// <param name="relative">The path under the home folder.</param>
    /// <param name="main">A path <see cref="Checkout"/> returned.</param>
    /// <returns>Its real path.</returns>
    protected abstract string Worktree(string relative, string main);

    /// <summary>A symbolic link at <paramref name="relative"/> to the folder <paramref name="target"/>.</summary>
    /// <param name="relative">The link's path under the home folder.</param>
    /// <param name="target">A folder's real path.</param>
    /// <returns>The link's path.</returns>
    protected abstract string Link(string relative, string target);

    /// <summary>The home folder of a tree made with no fixture, as a path the tree can join.</summary>
    /// <returns>The home folder.</returns>
    protected abstract string HomeFolder();

    /// <summary>A folder's children are its folders by name, links left out.</summary>
    [Fact]
    public void Folders_are_sorted_by_name_and_leave_links_out()
    {
        var b = Folder("top/b");
        var a = Folder("top/a");
        _ = Link("top/c", a);

        var children = Create().Folders(Folder("top"));

        Expect.Sequence([a, b], children);
    }

    /// <summary>A '.git' folder, a '.git' file and no '.git' are told apart.</summary>
    [Fact]
    public void A_git_folder_a_git_file_and_none_are_told_apart()
    {
        var main = Checkout("work/app", origin: null);
        var linked = Worktree("work/app-feat", main);
        var plain = Folder("work/notes");
        var tree = Create();

        Expect.Equal((GitMarker.Folder, GitMarker.File, GitMarker.None), (tree.Marker(main), tree.Marker(linked), tree.Marker(plain)));
    }

    /// <summary>A main checkout counts its linked worktrees; one without any has 0.</summary>
    [Fact]
    public void A_main_checkout_counts_its_linked_worktrees()
    {
        var main = Checkout("work/app", origin: null);
        _ = Worktree("work/app-one", main);
        _ = Worktree("work/app-two", main);
        var alone = Checkout("work/lib", origin: null);
        var tree = Create();

        Expect.Equal((2, 0), (tree.Worktrees(main), tree.Worktrees(alone)));
    }

    /// <summary>The origin comes back as git holds it, and a checkout without one answers null.</summary>
    /// <returns>When the reads are checked.</returns>
    [Fact]
    public async Task The_origin_is_the_url_git_holds_and_null_without_one()
    {
        var withOrigin = Checkout("work/app", "https://github.com/acme/app.git");
        var without = Checkout("work/lib", origin: null);
        var tree = Create();

        Expect.Equal("https://github.com/acme/app.git", await tree.Origin(withOrigin, Token));
        Expect.Equal(expected: null, actual: await tree.Origin(without, Token));
    }

    /// <summary>A folder reads as its real path; a missing one as null.</summary>
    [Fact]
    public void A_folder_is_real_and_a_missing_one_is_null()
    {
        var folder = Folder("work");
        var tree = Create();

        Expect.Equal(folder, tree.Real(folder));
        Expect.Equal(expected: null, actual: tree.Real(HomeFolder() + "/gone"));
    }
}
