using System.Globalization;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>The pin-suggest read (concept pins spec §3.6, #1667 Q7d): a concept's terms, each with the visible records its alias would newly link.</summary>
[Trait("Lane", "unit")]
public sealed class PinSuggestTests
{
    private const string Section = "{\"labeling\":{\"enabled\":false}}";
    private const string Profile = "{\"labeling\":{\"enabled\":false},\"sources\":{\"github\":{\"repos\":[\"acme/aw-split-panes\"]}}}";
    private static readonly RecordKey _linked = RecordKey.From("github:issue/acme/aw-split-panes/1");
    private static readonly RecordKey _second = RecordKey.From("github:issue/acme/aw-split-panes/2");
    private static readonly RecordKey _third = RecordKey.From("github:issue/acme/aw-split-panes/3");
    private static readonly RecordKey _removed = RecordKey.From("github:issue/acme/aw-split-panes/4");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Issue(RecordKey key, string title, string body, DateTimeOffset now) => new(
        key, RecordKind.Issue, title, body, Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("repo:acme/aw-split-panes"));

    // concept:billing carries the terms 'payment' and 'invoice'; three issues say "payments", the first is already linked to billing, and a fourth says "invoice" and is then removed at the source.
    private static async Task<MapWorld> Rig()
    {
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z", CultureInfo.InvariantCulture);
        var items = new[]
        {
            Issue(_linked, "Slow checkout", "Payments take ten seconds.", now),
            Issue(_second, "Declined cards", "Payments fail when the card is declined twice.", now),
            Issue(_third, "Refund wording", "The payments page says refunded.", now),
            Issue(_removed, "Overdue", "The invoice is overdue.", now),
        };
        var world = new MapWorld(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, items)));
        var graph = new ConceptGraph().ConceptWithTerms("concept:billing", "Billing", "payment", "invoice").Node("feature:checkout", "Checkout").Member("feature:checkout", "concept:billing").Build();
        var saved = await world.Store.SaveCurrent(new MapImport(ContextRepoName.From(FixtureMap.Repo), Commit: null, graph, []), now, Ct);
        Assert.True(saved.IsSuccess, saved.IsError ? saved.Error.Message : null);
        var synced = await world.Engine.Sync(source: null, MapWorld.HumanWith(Section), Ct);
        Assert.True(synced.IsSuccess, synced.IsError ? synced.Error.Message : null);
        var link = new IntentLink(_linked, NodeKey.From("concept:billing"), LinkRule.Identifier, 0.9, "00");
        Assert.True((await world.Intents.ReplaceRuleLinks(_linked, LinkRule.Identifier, [link], Ct)).IsSuccess);
        var gone = await world.Records.Commit(new RecordBatch(RecordSamples.Github, [], [_removed], SyncPosition.Start, now), Ct);
        Assert.True(gone.IsSuccess, gone.IsError ? gone.Error.Message : null);
        return world;
    }

    private static PinSuggestQuery ForConcept(string key) => new(MapRead.Current, Repo: null, NodeKey.From(key), Search: null, Limit: 50, Lines: null);

    [Fact]
    public async Task Suggest_counts_records_the_alias_rule_would_newly_link()
    {
        using var world = await Rig();

        var suggested = await world.Engine.PinSuggest(ForConcept("concept:billing"), MapWorld.HumanWith(Profile), Ct);

        Assert.True(suggested.IsSuccess, suggested.IsError ? suggested.Error.Message : null);
        var terms = suggested.Value.Terms;
        Assert.NotNull(terms);
        Assert.Equal([("payment", 2), ("invoice", 0)], terms.Select(t => (t.Word, t.NewRecords)));   // 3 records say "payments", 1 is linked already; the invoice one is removed
        Assert.Null(suggested.Value.Features);
        Assert.Null(suggested.Value.Matches);
    }
}
