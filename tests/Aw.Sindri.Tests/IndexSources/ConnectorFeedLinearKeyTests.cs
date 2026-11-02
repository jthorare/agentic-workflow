using Aw.Connectors.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Sindri.Host.IndexSources;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>Projects page spec §4.5, source 2: a pull request that names a Linear issue of a listed team ("ENG-12") in its title or text relates to that issue.</summary>
public sealed class ConnectorFeedLinearKeyTests
{
    private static readonly DateTimeOffset _t = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ConnectorItem Item(string title, string text, string kind = "pr", string key = "github:pr/acme/widgets/12") =>
        new(key, kind, title, text, "octocat", "https://github.com/acme/widgets/pull/12", "open", "open", _t, _t.AddHours(1), ClosedAt: null, "v1", [], "acme/widgets");

    private static async Task<RecordItem> Read(ConnectorItem item, IReadOnlyCollection<string>? teams)
    {
        var feed = new ConnectorFeed(SourceId.From("github"), new OnePage(item), new ProfileRepoMap(["acme/widgets"], [ContextRepoName.From("widgets")]).Place, linearTeams: teams);
        return Assert.Single((await feed.Pull(new RecordPull(After: null, Limit: 50, NotBefore: null), Ct)).Value.Items);
    }

    private static string[] KeyLinks(RecordItem item) =>
        [.. item.Links.Where(static l => l.ToRef.StartsWith("linear:issue-key/", StringComparison.Ordinal)).Select(static l => $"{l.Kind}:{l.ToRef}")];

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_naming_a_listed_teams_issue_relates_to_it_once()
    {
        var item = await Read(Item("ENG-12: fix save", "Fixes ENG-12.\n\nAlso touches ENG-40 and ENG-12 again."), ["ENG"]);

        Assert.Equal(["Relates:linear:issue-key/ENG-12", "Relates:linear:issue-key/ENG-40"], KeyLinks(item));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_of_a_team_not_listed_or_inside_a_longer_word_is_not_linked()
    {
        var item = await Read(Item("OPS-3 and UTF-8", "XENG-12, ENG-12a, feat/ENG-7-save"), ["ENG"]);

        Assert.Equal(["Relates:linear:issue-key/ENG-7"], KeyLinks(item));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_pull_requests_get_issue_key_links()
    {
        var item = await Read(Item("ENG-12 is broken", "see ENG-12", kind: "issue", key: "github:issue/acme/widgets/3"), ["ENG"]);

        Assert.Empty(KeyLinks(item));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_listed_teams_no_issue_key_is_linked()
    {
        var item = await Read(Item("ENG-12: fix save", "Fixes ENG-12."), []);

        Assert.Empty(KeyLinks(item));
    }

    // One page holding the item.
    private sealed class OnePage(ConnectorItem item) : IConnector
    {
        public ConnectorDescription Describe() => new("github", "GitHub");

        public ValueTask<Result<ConnectorPage>> Pull(Connectors.Core.SourceCursor? after, int limit, DateTimeOffset? before, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ConnectorPage>>(new ConnectorPage([item], [], new Connectors.Core.SourceCursor("next"), CursorReset: false));

        public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
