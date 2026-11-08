using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// The projector over a ledger the recipe, pipeline and scoping engines wrote (Sindri v2 PR 4b): their event kinds are history, so they
/// reach the item timeline and change nothing else, and 'sindri views rebuild' still reaches the head.
/// </summary>
public sealed class ProjectorRetiredKindsTests
{
    private static readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));
    private readonly SliceHarness _h = new();
    private readonly InMemoryViewStore _views = new(SliceHarness.Json);
    private readonly ListLogger<Projector> _log = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Retired_event_kinds_fold_as_timeline_entries_only()
    {
        var v = new ViewsHarness();
        await v.Fold(EventTypes.ItemCreated, new ItemCreated(v.Slice.Ids.New<CommandId>(), _item, "old work", Repo: null, Link: null, Body: null, Owner: null), _item);

        await v.Fold(EventTypes.WorkflowStarted, Dummy(), _item, static _ => """{"instance":"ITM-1/step/1","from":"","to":"produce","trigger":"start","outcome":null,"run":null}""");
        await v.Fold("line.phaseChanged", Dummy(), _item, static _ => """{"line":"L-1","phase":"build","to":"review"}""");
        await v.Fold("scope.frozen", Dummy(), _item, static _ => """{"packet":"P-1","hash":"sha256:bbbb"}""");
        await v.Fold("recipe.approved", Dummy(), _item, static _ => """{"id":"workflow.step","version":1,"hash":"sha256:aaaa"}""");

        var timeline = (await v.Views.Range<TimelineRow>(ViewNames.ItemTimeline, _item.Value + "/", _item.Value + "0", 10, Token)).Value;
        Assert.Equal(["item.created", "workflow.started", "line.phaseChanged", "scope.frozen", "recipe.approved"], timeline.Select(static t => t.Type), StringComparer.Ordinal);
        var row = (await v.Views.Get<ItemRow>(ViewNames.Items, _item.Value, Token)).Value!;
        Assert.Equal((ItemState.New, null, 1L), (row.State, row.Instance, row.Version));
        Assert.Empty((await v.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_over_an_old_ledger_ends_with_no_stall()
    {
        await Append(EventTypes.ItemCreated, new ItemCreated(_h.Ids.New<CommandId>(), _item, "old work", Repo: null, Link: null, Body: null, Owner: null), "c1");
        await Append(EventTypes.WorkflowStarted, Dummy(), "w1", static _ => """{"instance":"ITM-1/step/1","from":"","to":"produce","trigger":"start","outcome":null,"run":null}""");
        await Append("line.phaseChanged", Dummy(), "l1", static _ => """{"line":"L-1","phase":"build","to":"review"}""");
        await Append(EventTypes.ItemLinked, new ItemLinked(_h.Ids.New<CommandId>(), _item, RecordKey.From("linear:issue/abc-123"), ItemAlias.From("ENG-142")), "k1");
        await Append("recipe.approved", Dummy(), "r1", static _ => """{"id":"workflow.step","version":1,"hash":"sha256:aaaa"}""");
        var progress = new ProjectorProgress(_h.Clock);
        var projector = new Projector(new CountingLedgerReader(_h.Store), _views, Projections(), progress, _h.Callers, _h.Clock, _log);

        progress.RequestRebuild();
        var pass = await projector.CatchUp(Token);

        Assert.Equal(ProjectorPass.CaughtUp, pass);
        Assert.True(progress.Current is { Cursor: 5, LedgerHead: 5, Stalled: false, Rebuilding: false });
        var link = Assert.Single((await _views.List<ItemLinkRow>(ViewNames.ItemLinks, Token)).Value);
        Assert.Equal((_item, "ENG-142"), (link.Item, link.Ref));
    }

    private IReadOnlyList<IProjection> Projections() =>
    [
        new ItemsProjection(_h.Events, new PassThroughScrubber()),
        new ItemAliasesProjection(_h.Events),
        new ItemTimelineProjection(SliceHarness.Json, new PassThroughScrubber()),
        new ItemLinksProjection(_h.Events),
        new NeedsYouProjection(_h.Events, new PassThroughScrubber(), []),
    ];

    // any payload record: the tests overwrite its JSON with the text an old ledger holds
    private static WorkflowEventPayload Dummy() => new("ITM-1/step/1", "", "produce", "start", Outcome: null, Run: null);

    private async Task Append<TPayload>(string type, TPayload payload, string key, Func<string, string>? edit = null)
    {
        var grant = (await _h.Store.Read(Token)).Value is null ? await _h.HoldLease() : _h.Held.Current!;
        var entry = _h.Events.Entry(type, payload, key, _item.Value);
        if (edit is not null)
        {
            entry = entry with { Envelope = entry.Envelope with { Payload = edit(entry.Envelope.Payload) } };
        }

        _ = await _h.Store.Append(entry, grant, Token);
    }
}
