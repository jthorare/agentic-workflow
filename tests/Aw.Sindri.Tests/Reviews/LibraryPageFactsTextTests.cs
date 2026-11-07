using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;
using Aw.Sindri.Features.ReviewListFacts;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>How the library page writes a file and a finding (PR review page spec §6.1): the home folder as a tilde, the message then the fix, the record key swapped for the file.</summary>
[Trait("Lane", "unit")]
public sealed class LibraryPageFactsTextTests
{
    private static readonly LibraryHost _host = new() { Home = "/home/joi", StateDefault = "/home/joi/.agentic-workflow" };

    private static LibraryFolders Folders() =>
        LibraryFolders.Of(new LibrarySettings { Vaults = [new LibraryVault { Name = "vault", Path = "~/notes/vault" }, new LibraryVault { Name = "elsewhere", Path = "/srv/notes" }] }, _host);

    [Theory]
    [InlineData("doc:aw/acme-api/specs/q.md", "~/.agentic-workflow/acme-api/specs/q.md")]
    [InlineData("doc:vault/design/export.md", "~/notes/vault/design/export.md")]
    [InlineData("doc:elsewhere/a.md", "/srv/notes/a.md")]
    public void A_doc_key_is_written_as_the_file_it_names(string key, string file) =>
        Assert.Equal(file, Folders().File(key));

    [Fact]
    public void A_key_of_a_root_the_profile_no_longer_has_stays_as_it_is() =>
        Assert.Equal("doc:gone/a.md", Folders().File("doc:gone/a.md"));

    [Fact]
    public void A_root_is_written_as_its_folder_and_an_unknown_one_by_its_name()
    {
        var folders = Folders();

        Assert.Equal(("~/notes/vault", "gone"), (folders.Root("vault"), folders.Root("gone")));
    }

    [Fact]
    public void A_finding_reads_message_then_fix_with_the_file_in_place_of_the_key()
    {
        var error = new DesignDocError("SND-LIB-010", Line: 3, "doc:vault/design/export.md line 3: a missing or bad front-matter key: kind is missing.", "Write the key as the table above says.");

        var text = LibraryErrorText.Of(error, "doc:vault/design/export.md", "~/notes/vault/design/export.md");

        Assert.Equal("~/notes/vault/design/export.md line 3: a missing or bad front-matter key: kind is missing. Write the key as the table above says", text);
    }

    [Fact]
    public void A_finding_Context_wrote_on_a_line_reads_path_line_n()
    {
        const string Key = "doc:aw/acme-api/plans/booking-retry-tdd.md";
        var error = new DesignDocError("SND-LIB-010", Line: 4, Key + ":4: status 'draft2' isn't allowed.", "Use draft.");

        Assert.Equal("~/.agentic-workflow/acme-api/plans/booking-retry-tdd.md line 4: status 'draft2' isn't allowed. Use draft", LibraryErrorText.Of(error, Key, "~/.agentic-workflow/acme-api/plans/booking-retry-tdd.md"));
    }

    [Fact]
    public void A_finding_with_no_line_keeps_its_message_and_no_fix_adds_nothing()
    {
        var error = new DesignDocError("SND-LIB-018", Line: null, "doc:vault/p.md: too large: the file is over 1 MiB.", Fix: "");

        Assert.Equal("~/p.md: too large: the file is over 1 MiB.", LibraryErrorText.Of(error, "doc:vault/p.md", "~/p.md"));
    }

    [Fact]
    public void A_finding_with_no_line_and_a_fix_joins_them_and_replaces_the_key_wherever_it_stands()
    {
        var error = new DesignDocError("SND-LIB-012", Line: null, "doc id \"a\" is also used by doc:vault/b.md.", "Rename one of them.");

        Assert.Equal("doc id \"a\" is also used by ~/v/b.md. Rename one of them", LibraryErrorText.Of(error, "doc:vault/b.md", "~/v/b.md"));
    }

    [Theory]
    [InlineData("/home/joi/a.md", "~/a.md")]
    [InlineData("/home/joiner/a.md", "/home/joiner/a.md")]
    public void The_home_folder_is_written_as_a_tilde_only_at_a_folder_boundary(string path, string shown) =>
        Assert.Equal(shown, Folders().Show(path));
}
