using System.Text.Json;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>
/// The repo docs feed shows every code tests/fixtures/library/expected.json names for a file under broken/ (#2199), as the library feed does (#2181):
/// the contract is driven from the JSON, so a code added to it that this feed does not decide fails here.
/// </summary>
[Trait("Lane", "integration")]
public sealed class RepoDocsFeedFixtureParityTests
{
    private static readonly string[] _globs = ["docs/**/*.md"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_code_the_fixture_contract_names_for_a_broken_doc_shows_on_that_doc_in_the_library_read_of_the_repo_docs_feed()
    {
        var folder = Path.Combine(LibraryFeedRig.RepoRoot(), "tests", "fixtures", "library");
        using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "expected.json"), Ct));
        var expected = contract.RootElement.GetProperty("files").EnumerateArray()
            .Select(static f => (Path: f.GetProperty("path").GetString()!, Codes: f.GetProperty("errors").EnumerateArray().Select(static e => e.GetString()!).ToList(), Source: f.TryGetProperty("source", out var source) ? source.GetString() : null))
            .Where(static f => f.Path.StartsWith("broken/", StringComparison.Ordinal) && f.Path.EndsWith(".md", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(expected);

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, _, source) in expected)
        {
            files["docs/" + path] = await File.ReadAllTextAsync(Path.Combine(folder, path), Ct);
            if (source is not null)
            {
                files["docs/" + source] = await File.ReadAllTextAsync(Path.Combine(folder, source), Ct);   // the generated doc's packet.json, beside it
            }
        }

        using var home = new TempHome();
        using var git = new TempGitRepo(files);
        var store = new SqliteRecordStore(home.Database);
        string[] github = ["acme/" + git.Repo.Name.Value];   // the profile's synced repo, so the docs carry the repo's audience
        var feed = new RepoDocsFeed([git.Repo], _globs, github, store, home.Git, home.Scrubbers);
        _ = await FeedRun.Once(feed, store, home.Scrubbers);

        var own = ProfileAudiencePolicy.OwnRepo(git.Repo.Name, github);
        Assert.True(own is not null, "the repo has no audience");
        var docs = (await new ContextLibrary(new SqliteLibraryStore(home.Database)).ListDocs(new LibraryDocFilter(), new AudienceScope(new HashSet<AudienceKey> { own.Value }), Ct)).Value;

        foreach (var (path, codes, _) in expected)
        {
            var shown = docs.SingleOrDefault(d => d.Key.Value.EndsWith("/docs/" + path, StringComparison.Ordinal));
            Assert.True(shown is not null, $"{path} is not in the library read");
            foreach (var code in codes)
            {
                Assert.True(shown.Errors.Any(e => e.Code == code), $"{path}: {code} is not on the doc (shown: {string.Join(", ", shown.Errors.Select(static e => e.Code))})");
            }
        }
    }
}
