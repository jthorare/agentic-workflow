using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.RelinkPinAliases;

/// <summary>
/// The pin-alias relink (concept pins spec §3.4, #1667 Q9): records already stored and linked get their alias links from the approved pins' aliases, by the alias rule alone,
/// without a model call and without touching any other link or any record's map version.
/// </summary>
[Trait("Lane", "unit")]
public sealed class RelinkPinAliasesTests
{
    private const string Section = "{\"labeling\":{\"enabled\":false}}";
    private static readonly RecordKey _payments = RecordKey.From("github:issue/acme/shop/1");
    private static readonly RecordKey _invoices = RecordKey.From("github:issue/acme/shop/2");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Issue(RecordKey key, string title, string body, DateTimeOffset now) => new(
        key, RecordKind.Issue, title, body, Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("repo:acme/shop"));

    // The map has the billing concept and the layout feature; two stored issues, one naming "payments", one "invoices".
    private static async Task<MapWorld> Rig()
    {
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z", CultureInfo.InvariantCulture);
        var items = new[]
        {
            Issue(_payments, "Retry failed charges", "Payments fail when the card is declined twice.", now),
            Issue(_invoices, "Stale totals", "Invoices show last month's total.", now),
        };
        var world = new MapWorld(feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, items)));
        _ = await world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"concept:billing\", label: Billing}", "  - {key: \"feature:layout\", label: Layout}",
            "edges:", "  - {from: \"feature:layout\", to: \"concept:billing\", type: belongs-to}"));
        var synced = await world.Engine.Sync(source: null, MapWorld.HumanWith(Section), Ct);
        Assert.True(synced.IsSuccess, synced.IsError ? synced.Error.Message : null);
        return world;
    }

    private static ContextScope Pinned(string id, params string[] aliases)
    {
        var pin = ConceptPin.Of(ConceptId.From(id), id, summary: null, features: [], paths: []).WithAliases(aliases);
        return MapWorld.HumanWith(Section, [.. MapWorld.Repos.Select((r, i) => i == 0 ? r with { Concepts = [pin] } : r)]);
    }

    private static async Task<List<IntentLink>> AliasLinks(MapWorld world) =>
        [.. (await world.Intents.Layer(Ct)).Value.Links.Where(l => l.Rule == LinkRule.Alias)];

    [Fact]
    public async Task A_stored_record_naming_an_alias_mentions_the_pin_after_a_relink()
    {
        using var world = await Rig();
        Assert.Empty(await AliasLinks(world));

        var relinked = await world.Engine.RelinkPinAliases(Pinned("billing", "payments"), Ct);

        Assert.True(relinked.IsSuccess, relinked.IsError ? relinked.Error.Message : null);
        var link = Assert.Single(await AliasLinks(world));   // expected 1 alias link: the payments issue, not the invoices one
        Assert.Equal((_payments, "concept:billing"), (link.Record, link.Target.Value));
    }

    [Fact]
    public async Task A_relink_changes_no_other_link_and_no_map_version_and_asks_no_model()
    {
        using var world = await Rig();
        List<IntentLink> before = [.. (await world.Intents.Layer(Ct)).Value.Links.Where(l => l.Rule != LinkRule.Alias)];
        var source = RecordSamples.Github;
        var version = (await world.Engine.RelinkPinAliases(Pinned("billing"), Ct)).Value.MapVersion;
        var linkedBefore = (await world.Intents.LinkedUnder(source, version, Ct)).Value;
        var requests = world.Model.Requests.Count;

        var relinked = await world.Engine.RelinkPinAliases(Pinned("billing", "payments", "invoices"), Ct);

        Assert.True(relinked.IsSuccess, relinked.IsError ? relinked.Error.Message : null);
        Assert.Equal(before, [.. (await world.Intents.Layer(Ct)).Value.Links.Where(l => l.Rule != LinkRule.Alias)]);
        Assert.Equal(linkedBefore, (await world.Intents.LinkedUnder(source, version, Ct)).Value);
        Assert.Equal(requests, world.Model.Requests.Count);   // expected 0 new model calls
    }

    [Fact]
    public async Task Removing_an_alias_removes_only_its_link()
    {
        using var world = await Rig();
        _ = await world.Engine.RelinkPinAliases(Pinned("billing", "payments", "invoices"), Ct);
        Assert.Equal(2, (await AliasLinks(world)).Count);

        _ = await world.Engine.RelinkPinAliases(Pinned("billing", "invoices"), Ct);

        Assert.Equal(_invoices, Assert.Single(await AliasLinks(world)).Record);
    }

    [Fact]
    public async Task A_pin_with_no_concept_node_in_the_map_gets_no_links()
    {
        using var world = await Rig();

        var relinked = await world.Engine.RelinkPinAliases(Pinned("ghost", "payments"), Ct);

        Assert.True(relinked.IsSuccess, relinked.IsError ? relinked.Error.Message : null);
        Assert.Empty(await AliasLinks(world));
        Assert.Equal(new PinLinkCount(ConceptId.From("ghost"), 0, 0), Assert.Single(relinked.Value.Pins));
    }

    [Fact]
    public async Task The_result_counts_the_records_scanned_changed_and_each_pins_linked_records_before_and_after()
    {
        using var world = await Rig();
        var scope = Pinned("billing", "payments");

        var first = (await world.Engine.RelinkPinAliases(scope, Ct)).Value;
        var again = (await world.Engine.RelinkPinAliases(scope, Ct)).Value;

        Assert.Equal((2, 1), (first.Records, first.Changed));
        Assert.Equal(new PinLinkCount(ConceptId.From("billing"), 0, 1), Assert.Single(first.Pins));
        Assert.Equal(PinAliasSet.Hash(PinAliasSet.Of(scope).Value), first.AliasesHash);
        Assert.Equal((2, 0), (again.Records, again.Changed));   // nothing differs the second time: nothing is written
        Assert.Equal(new PinLinkCount(ConceptId.From("billing"), 1, 1), Assert.Single(again.Pins));
    }
}
