using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>SND-LIB-015 and -016 are decided on every read, not stored (#2181): a doc added later clears the error of the doc that needed it, and removing it brings the error back.</summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedScopeStaleTests
{
    private static readonly AudienceScope _owner = new(new HashSet<AudienceKey> { ProfileAudiencePolicy.LibraryDoc([], ["acme/harbor-web"]) });

    private const string Defines = "---\naw-doc: 1\nid: defs-doc\nkind: spec\ntitle: \"Defs\"\nstatus: draft\n---\n\n# Defs\n\n## Requirements\n\n- REQ-99 A requirement.\n";

    private const string Parent = "---\naw-doc: 1\nid: no-such-doc\nkind: note\ntitle: \"The parent\"\nstatus: draft\n---\n\n# The parent\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_doc_that_defines_the_missing_item_lands_later_and_clears_the_015_then_removing_it_brings_it_back()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write("015-broken-ref.md", await Fixture("015-broken-ref.md"));
        var library = new ContextLibrary(new SqliteLibraryStore(home.Database));

        await Feed(home, rig);
        Assert.Contains(await ErrorCodes(library, "broken-ref"), static c => c == DesignDocCodes.BrokenRef);

        var defs = rig.Write("defs-doc.md", Defines);
        await Feed(home, rig);
        Assert.DoesNotContain(await ErrorCodes(library, "broken-ref"), static c => c == DesignDocCodes.BrokenRef);

        File.Delete(defs);
        await Feed(home, rig);
        Assert.Contains(await ErrorCodes(library, "broken-ref"), static c => c == DesignDocCodes.BrokenRef);
    }

    [Fact]
    public async Task A_doc_that_is_the_missing_parent_lands_later_and_clears_the_016()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write("016-unknown-parent.md", await Fixture("016-unknown-parent.md"));
        var library = new ContextLibrary(new SqliteLibraryStore(home.Database));

        await Feed(home, rig);
        Assert.Contains(await ErrorCodes(library, "unknown-parent"), static c => c == DesignDocCodes.UnknownDocLink);

        _ = rig.Write("no-such-doc.md", Parent);
        await Feed(home, rig);
        Assert.DoesNotContain(await ErrorCodes(library, "unknown-parent"), static c => c == DesignDocCodes.UnknownDocLink);
    }

    private static async Task<string> Fixture(string name) => await File.ReadAllTextAsync(Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", name), Ct);

    private static async Task Feed(TempHome home, LibraryFeedRig rig) => _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

    private static async Task<IReadOnlyList<string>> ErrorCodes(ContextLibrary library, string slug)
    {
        var docs = (await library.ListDocs(new LibraryDocFilter(), _owner, Ct)).Value;
        return [.. docs.Single(d => d.Slug == slug).Errors.Select(static e => e.Code)];
    }
}
