using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>How the library page orders and words its roots (Settings → Library).</summary>
[Trait("Lane", "unit")]
public sealed class LibraryRootsTests
{
    private static LibraryRootFacts Root(string name, LibraryRootKind kind, string? repo = null, bool off = false) =>
        new(name, kind, "~/" + name, repo, Deny: [], off, ScannedAt: null, Docs: 0, Errors: 0, Unreadable: null);

    [Fact]
    public void The_aw_root_comes_first_then_the_vaults_then_the_repos_each_in_the_order_given()
    {
        var rows = LibraryRoots.Assemble(
        [
            Root("repo:acme/api", LibraryRootKind.Repo, "acme/api"), Root("work", LibraryRootKind.Vault), Root("repo:acme/web", LibraryRootKind.Repo, "acme/web"), Root("inbox", LibraryRootKind.Vault), Root("aw", LibraryRootKind.Aw),
        ]);

        Assert.Equal(["aw", "work", "inbox", "repo:acme/api", "repo:acme/web"], rows.Select(static r => r.Name), StringComparer.Ordinal);
        Assert.Equal(["aw", "vault", "vault", "repo", "repo"], rows.Select(static r => r.Kind), StringComparer.Ordinal);
    }

    [Fact]
    public void Only_a_repo_root_has_a_scope_which_names_its_repo_and_its_docs_folder()
    {
        var rows = LibraryRoots.Assemble([Root("aw", LibraryRootKind.Aw), Root("work", LibraryRootKind.Vault), Root("repo:acme/api", LibraryRootKind.Repo, "acme/api")]);

        Assert.Equal([null, null, "acme/api · docs/**"], rows.Select(static r => r.Scope), StringComparer.Ordinal);
    }

    [Fact]
    public void A_root_is_on_unless_it_is_turned_off()
    {
        var rows = LibraryRoots.Assemble([Root("aw", LibraryRootKind.Aw, off: true), Root("work", LibraryRootKind.Vault)]);

        Assert.Equal([false, true], rows.Select(static r => r.On));
    }

    [Fact]
    public void The_counts_the_scan_time_and_the_reason_pass_through()
    {
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var facts = new LibraryRootFacts("notes", LibraryRootKind.Vault, "~/notes", Repo: null, ["Private/**"], Off: false, at, Docs: 12, Errors: 2, "permission denied");

        var row = Assert.Single(LibraryRoots.Assemble([facts]));

        Assert.Equal(("notes", "vault", "~/notes", null, true, at, 12, 2, "permission denied"), (row.Name, row.Kind, row.Path, row.Scope, row.On, row.ScannedAt, row.Docs, row.Errors, row.Unreadable));
        Assert.Equal(["Private/**"], row.Deny, StringComparer.Ordinal);
    }

    [Fact]
    public void No_roots_is_an_empty_list() =>
        Assert.Empty(LibraryRoots.Assemble([]));
}
