using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>SND-LIB-017 on the library page: a generated doc whose source-sha256 no longer matches the packet.json beside it must show the error after a feed pass, including when only packet.json changed (#2184).</summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedStaleGeneratedTests
{
    private static readonly AudienceScope _owner = new(new HashSet<AudienceKey> { ProfileAudiencePolicy.LibraryDoc([], ["acme/harbor-web"]) });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_generated_doc_whose_packet_json_beside_it_hashes_differently_is_017_on_the_library_read()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write("gen/packet.json", """{"n":1}""");
        _ = rig.Write("gen/doc.md", Doc(new string('a', 64)));

        var doc = await FeedAndReadSingle(home, rig);

        Assert.Contains(doc.Errors, static e => e.Code == DesignDocCodes.StaleGenerated);
    }

    [Fact]
    public async Task A_generated_doc_whose_packet_json_matches_has_no_017()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        var packet = rig.Write("gen/packet.json", """{"n":1}""");
        _ = rig.Write("gen/doc.md", Doc(LibraryFeedRig.Sha256(packet)));

        var doc = await FeedAndReadSingle(home, rig);

        Assert.DoesNotContain(doc.Errors, static e => e.Code == DesignDocCodes.StaleGenerated);
    }

    [Fact]
    public async Task Editing_only_the_packet_json_makes_the_next_pass_report_017()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        var packet = rig.Write("gen/packet.json", """{"n":1}""");
        _ = rig.Write("gen/doc.md", Doc(LibraryFeedRig.Sha256(packet)));
        Assert.DoesNotContain((await FeedAndReadSingle(home, rig)).Errors, static e => e.Code == DesignDocCodes.StaleGenerated);

        _ = rig.Write("gen/packet.json", """{"n":2}""");

        Assert.Contains((await FeedAndReadSingle(home, rig)).Errors, static e => e.Code == DesignDocCodes.StaleGenerated);
    }

    private static string Doc(string sourceSha) => string.Join(
        '\n',
        "---",
        "aw-doc: 1",
        "id: stale-generated",
        "kind: tdd",
        "title: \"Generated file\"",
        "status: draft",
        "generated: write_packet.py",
        $"source-sha256: {sourceSha}",
        "---",
        "",
        "# Generated",
        "",
        "## Overview",
        "");

    private static async Task<LibraryDoc> FeedAndReadSingle(TempHome home, LibraryFeedRig rig)
    {
        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);
        var library = new ContextLibrary(new SqliteLibraryStore(home.Database));
        return Assert.Single((await library.ListDocs(new LibraryDocFilter(), _owner, Ct)).Value);
    }
}
