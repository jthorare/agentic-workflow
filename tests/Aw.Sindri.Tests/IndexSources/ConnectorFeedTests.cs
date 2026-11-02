using Aw.Connectors.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Sindri.Host.IndexSources;

namespace Aw.Sindri.Tests.IndexSources;

public sealed class ConnectorFeedTests
{
    private static readonly DateTimeOffset _t = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ConnectorItem Item(string repo, string key = "github:pr/acme/widgets/12", string kind = "pr", string state = "merged", DateTimeOffset? updated = null, params ConnectorLink[] links) =>
        new(key, kind, "Fix save", "Fix save\n\nbody", "octocat", "https://github.com/acme/widgets/pull/12", state, state, _t, updated ?? _t.AddHours(1), ClosedAt: null, "v1", links, repo);

    private static ConnectorFeed Feed(StubConnector connector, string[]? listed = null, string[]? profile = null, Scrubbers? scrubbers = null) =>
        new(SourceId.From("github"), connector, new ProfileRepoMap(listed ?? ["acme/widgets"], (profile ?? ["widgets"]).Select(ContextRepoName.From)).Place, scrubbers);

    private static RecordPull Pull(DateTimeOffset? notBefore = null) => new(After: null, Limit: 50, notBefore);

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_becomes_a_record_of_the_profile_repo_with_a_repo_audience()
    {
        var connector = new StubConnector(Item("acme/widgets", links: [new ConnectorLink("closes", "github:issue/acme/widgets/3"), new ConnectorLink("touches", "path:acme/widgets/src/Save.cs")]));

        var page = (await Feed(connector).Pull(Pull(), Ct)).Value;

        var item = Assert.Single(page.Items);
        Assert.Equal("github:pr/acme/widgets/12", item.Key.Value);
        Assert.Equal(RecordKind.Pr, item.Kind);
        Assert.Equal(RecordState.Merged, item.State);
        Assert.Equal("widgets", item.Repo?.Value);
        Assert.Equal("repo:acme/widgets", item.Audience?.Value);
        Assert.Contains(item.Links, static l => l.Kind == RecordLinkKind.Closes && l.ToRef == "github:issue/acme/widgets/3");
        Assert.Contains(item.Links, static l => l.Kind == RecordLinkKind.Touches && l.ToRef == "path:widgets/src/Save.cs");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_of_a_repo_the_profile_does_not_list_is_dropped_and_counted()
    {
        var connector = new StubConnector(Item("acme/widgets"), Item("evil/other", key: "github:pr/evil/other/1"));
        var feed = Feed(connector);

        var page = (await feed.Pull(Pull(), Ct)).Value;

        Assert.True(page.Items.Select(static i => i.Key.Value).SequenceEqual(["github:pr/acme/widgets/12"], StringComparer.Ordinal));
        Assert.Equal(1, feed.SkippedNotInProfile);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_listed_repo_without_a_profile_repo_of_that_name_is_dropped()
    {
        var feed = Feed(new StubConnector(Item("acme/widgets")), profile: ["somethingelse"]);

        var page = (await feed.Pull(Pull(), Ct)).Value;

        Assert.Empty(page.Items);
        Assert.Equal(1, feed.SkippedNotInProfile);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_older_than_the_window_is_not_returned()
    {
        var connector = new StubConnector(Item("acme/widgets", updated: _t.AddDays(-400)));

        var page = (await Feed(connector).Pull(Pull(notBefore: _t.AddDays(-180)), Ct)).Value;

        Assert.Empty(page.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_cursor_goes_to_the_connector_and_back_as_the_connector_made_it()
    {
        var connector = new StubConnector(Item("acme/widgets"));

        var page = (await Feed(connector).Pull(new RecordPull(Context.Core.Sources.SourceCursor.From("acme/widgets|1|2"), 10, NotBefore: null), Ct)).Value;

        Assert.Equal("acme/widgets|1|2", connector.SeenCursor);
        Assert.Equal("next-cursor", page.Next.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_connector_error_comes_back_as_the_feeds_error()
    {
        var connector = new StubConnector { Failure = ConnectorsErrors.RateLimited.With("lifts at 14:05") };

        var result = await Feed(connector).Pull(Pull(), Ct);

        Assert.True(result.IsError);
        Assert.Equal(ConnectorsErrors.RateLimited.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_secret_in_a_title_or_text_is_cleaned_before_the_record_leaves_the_feed()
    {
        var dirty = Item("acme/widgets") with { Title = "Rotate SECRETVALUE now", Text = "key is SECRETVALUE here" };
        var scrubbers = new Scrubbers(new MaskingFilter(), new MaskingFilter(), ScrubRuleset.From("0123456789abcdef"));

        var page = (await Feed(new StubConnector(dirty), scrubbers: scrubbers).Pull(Pull(), Ct)).Value;

        var item = Assert.Single(page.Items);
        Assert.DoesNotContain("SECRETVALUE", item.Title + item.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_can_place_its_records_with_its_own_audience()
    {
        var feed = new ConnectorFeed(SourceId.From("slack"), new StubConnector(Item("ignored", key: "slack:C123/1700.1", kind: "message", state: "open")),
            static i => new Placement(Repo: null, AudienceKey.From("slack:" + i.Key["slack:".Length..i.Key.IndexOf('/', StringComparison.Ordinal)]), SourceRepo: null));

        var item = Assert.Single((await feed.Pull(Pull(), Ct)).Value.Items);

        Assert.Equal("slack:C123", item.Audience?.Value);
        Assert.Null(item.Repo);
        Assert.Equal(RecordKind.Message, item.Kind);
    }

    private sealed class MaskingFilter : IIngestFilter, IEgressScrubber
    {
        public ScrubbedField Clean(string raw, bool singleLine) => new(raw.Replace("SECRETVALUE", "[redacted]", StringComparison.Ordinal), Redacted: true, Blocked: false);

        public ScrubbedField Scrub(string value) => Clean(value, singleLine: false);
    }

    private sealed class StubConnector(params ConnectorItem[] items) : IConnector
    {
        public Error? Failure { get; init; }

        public string? SeenCursor { get; private set; }

        public ConnectorDescription Describe() => new("github", "GitHub");

        public ValueTask<Result<ConnectorPage>> Pull(Connectors.Core.SourceCursor? after, int limit, DateTimeOffset? before, CancellationToken cancellationToken)
        {
            SeenCursor = after?.Value;
            return ValueTask.FromResult<Result<ConnectorPage>>(Failure is { } failure
                ? failure
                : new ConnectorPage(items, [], new Aw.Connectors.Core.SourceCursor("next-cursor"), CursorReset: false));
        }

        public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
