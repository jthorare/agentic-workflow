using System.Text.Json;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>
/// The feed shows every code tests/fixtures/library/expected.json names for a file under broken/ (#2181): the contract is driven from the JSON, so a code added to it that the feed does not decide fails here.
/// The parser's own fixture test cannot see this, because 012, 015 and 016 are decided against the whole scope.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedFixtureParityTests
{
    private static readonly AudienceScope _owner = new(new HashSet<AudienceKey> { ProfileAudiencePolicy.LibraryDoc([], ["acme/harbor-web"]) });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_code_the_fixture_contract_names_for_a_broken_doc_shows_on_that_doc_in_the_library_read()
    {
        var folder = Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library");
        using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "expected.json"), Ct));
        var expected = contract.RootElement.GetProperty("files").EnumerateArray()
            .Select(static f => (Path: f.GetProperty("path").GetString()!, Codes: f.GetProperty("errors").EnumerateArray().Select(static e => e.GetString()!).ToList(), Source: f.TryGetProperty("source", out var source) ? source.GetString() : null))
            .Where(static f => f.Path.StartsWith("broken/", StringComparison.Ordinal) && f.Path.EndsWith(".md", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(expected);

        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        foreach (var (path, _, source) in expected)
        {
            _ = rig.Write(path, await File.ReadAllTextAsync(Path.Combine(folder, path), Ct));
            if (source is not null)
            {
                _ = rig.Write(source, await File.ReadAllTextAsync(Path.Combine(folder, source), Ct));   // the generated doc's packet.json, beside it
            }
        }

        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);
        var docs = (await new ContextLibrary(new SqliteLibraryStore(home.Database)).ListDocs(new LibraryDocFilter(), _owner, Ct)).Value;

        foreach (var (path, codes, _) in expected)
        {
            var shown = docs.SingleOrDefault(d => d.Key.Value.EndsWith("/" + path, StringComparison.Ordinal));
            Assert.True(shown is not null, $"{path} is not in the library read");
            foreach (var code in codes)
            {
                Assert.True(shown.Errors.Any(e => e.Code == code), $"{path}: {code} is not on the doc (shown: {string.Join(", ", shown.Errors.Select(static e => e.Code))})");
            }
        }
    }
}
