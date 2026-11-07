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

        var text = ContextLibraryPageFacts.Text(error, "doc:vault/design/export.md", "~/notes/vault/design/export.md");

        Assert.Equal("~/notes/vault/design/export.md line 3: a missing or bad front-matter key: kind is missing. Write the key as the table above says", text);
    }
}
