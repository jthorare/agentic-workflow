using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>The scope-wide design-doc errors (SND-LIB-015, -016) on the library page: the feed stores per-file errors only, so the read has to show the two a whole vault decides (#2181).</summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedScopeErrorsTests
{
    private static readonly AudienceScope _owner = new(new HashSet<AudienceKey> { ProfileAudiencePolicy.LibraryDoc([], ["acme/harbor-web"]) });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_ref_to_an_item_no_doc_of_the_scope_defines_is_015_on_the_library_read()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        var library = await FeedAndOpen(home, rig, "015-broken-ref.md");

        var doc = Assert.Single((await library.ListDocs(new LibraryDocFilter(), _owner, Ct)).Value);

        Assert.Contains(doc.Errors, static e => e.Code == DesignDocCodes.BrokenRef);
        Assert.Equal(["proof", "TP-1"], (await library.Items(doc.Key, _owner, Ct)).Value.Select(static i => i.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_parent_or_supersedes_naming_no_doc_of_the_scope_is_016_on_the_library_read()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        var library = await FeedAndOpen(home, rig, "016-unknown-parent.md", "016-unknown-supersedes.md");

        var docs = (await library.ListDocs(new LibraryDocFilter(), _owner, Ct)).Value;

        Assert.Equal(2, docs.Count);
        Assert.All(docs, static d => Assert.Contains(d.Errors, static e => e.Code == DesignDocCodes.UnknownDocLink));
        foreach (var doc in docs)
        {
            Assert.Equal(["section"], (await library.Items(doc.Key, _owner, Ct)).Value.Select(static i => i.Key), StringComparer.Ordinal);
        }
    }

    private static async Task<ContextLibrary> FeedAndOpen(TempHome home, LibraryFeedRig rig, params string[] fixtures)
    {
        foreach (var name in fixtures)
        {
            _ = rig.Write(name, await File.ReadAllTextAsync(Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", name), Ct));
        }

        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);
        return new ContextLibrary(new SqliteLibraryStore(home.Database));
    }
}
