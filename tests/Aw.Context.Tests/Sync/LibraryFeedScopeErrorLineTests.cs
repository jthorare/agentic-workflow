using System.Globalization;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>The SND-LIB-015 the library read adds from the stored scope check names the ref's real line, the one the parser gives, never a made-up 0 (#2189).</summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedScopeErrorLineTests
{
    private const string Fixture = "015-broken-ref.md";

    private static readonly AudienceScope _owner = new(new HashSet<AudienceKey> { ProfileAudiencePolicy.LibraryDoc([], ["acme/harbor-web"]) });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_015_on_the_library_read_names_the_refs_real_line_not_0()
    {
        var text = await File.ReadAllTextAsync(Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library", "broken", Fixture), Ct);
        var parsed = DesignDocParser.Parse(Fixture, text);
        var expected = Assert.Single(DesignDocScope.Check([new ScopedDesignDoc(Fixture, "acme/harbor-web", parsed)]), static f => f.Error.Code == DesignDocCodes.BrokenRef).Error.Line;
        Assert.True(expected > 0);

        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write(Fixture, text);
        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);
        var library = new ContextLibrary(new SqliteLibraryStore(home.Database));

        var doc = Assert.Single((await library.ListDocs(new LibraryDocFilter(), _owner, Ct)).Value);
        var error = Assert.Single(doc.Errors, static e => e.Code == DesignDocCodes.BrokenRef);

        Assert.DoesNotContain(":0:", error.Message, StringComparison.Ordinal);
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $":{expected}:"), error.Message, StringComparison.Ordinal);
        Assert.Equal(expected, error.Line);
    }
}
