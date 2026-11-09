using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// The projector over a ledger the recipe, pipeline and scoping engines wrote (Sindri v2 PR 4b): their event kinds are history, so they
/// reach the item timeline and change nothing else, and 'sindri views rebuild' still reaches the head.
/// </summary>
public sealed class ProjectorRetiredKindsTests
{
    private static readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-di-test");
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
        await v.Fold(EventTypes.WorkflowRetired, Dummy(), _item, static _ => """{"instance":"ITM-1/step/1","from":"","to":"retired","trigger":"retire","outcome":"retired","run":null}""");

        var timeline = (await v.Views.Range<TimelineRow>(ViewNames.ItemTimeline, _item.Value + "/", _item.Value + "0", 10, Token)).Value;
        Assert.Equal(["item.created", "workflow.started", "line.phaseChanged", "scope.frozen", "recipe.approved", "workflow.retired"], timeline.Select(static t => t.Type), StringComparer.Ordinal);
        Assert.Equal(TimelineLine.WorkflowRetired, timeline[^1].Line);
        var row = (await v.Views.Get<ItemRow>(ViewNames.Items, _item.Value, Token)).Value!;
        Assert.Equal((ItemState.New, null, 1L), (row.State, row.Instance, row.Version));
        Assert.Empty((await v.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_over_an_old_ledger_ends_with_no_stall()
    {
        var head = await AppendOldLedger();
        var progress = new ProjectorProgress(_h.Clock);
        var projector = new Projector(new CountingLedgerReader(_h.Store), _views, Projections(), progress, _h.Callers, _h.Clock, _log);

        progress.RequestRebuild();
        var pass = await projector.CatchUp(Token);

        Assert.Equal(ProjectorPass.CaughtUp, pass);
        Assert.True(progress.Current is { Stalled: false, Rebuilding: false } && progress.Current.Cursor == head && progress.Current.LedgerHead == head);
        var link = Assert.Single((await _views.List<ItemLinkRow>(ViewNames.ItemLinks, Token)).Value);
        Assert.Equal((_item, "ENG-142"), (link.Item, link.Ref));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_over_an_old_ledger_through_every_registered_projection_ends_with_no_stall()
    {
        var head = await AppendOldLedger();
        await using var provider = Compose().BuildServiceProvider();
        var progress = new ProjectorProgress(_h.Clock);
        var projector = new Projector(new CountingLedgerReader(_h.Store), _views, [.. provider.GetServices<IProjection>()], progress, _h.Callers, _h.Clock, _log);

        progress.RequestRebuild();
        var pass = await projector.CatchUp(Token);

        Assert.Equal(ProjectorPass.CaughtUp, pass);
        Assert.True(progress.Current is { Stalled: false, Rebuilding: false } && progress.Current.Cursor == head && progress.Current.LedgerHead == head);
    }

    private static IServiceCollection Compose()
        => Composition.Compose(
            new ServiceCollection(),
            name => name switch { "AW_STATE_DIR" => _stateRoot, _ => null },
            new FakeTimeProvider());

    // every retired kind the engines wrote, at least once, between an item.created and an item.linked; returns the ledger head
    private async Task<long> AppendOldLedger()
    {
        const string step = "ITM-1/step/1";
        static string Row(string from, string to, string trigger, string? outcome) =>
            "{\"instance\":\"" + step + "\",\"from\":\"" + from + "\",\"to\":\"" + to + "\",\"trigger\":\"" + trigger + "\",\"outcome\":" + (outcome is null ? "null" : "\"" + outcome + "\"") + ",\"run\":null}";
        var hash = "sha256:" + new string('a', 64);
        (string Type, string Json)[] rows =
        [
            (EventTypes.WorkflowStarted, Row("", "produce", "start", outcome: null)),
            (EventTypes.WorkflowTransition, Row("produce", "review", "report", outcome: null)),
            (EventTypes.WorkflowRefused, Row("produce", "produce", "report", outcome: null)),
            (EventTypes.WorkflowNotified, Row("review", "review", "notify", outcome: null)),
            (EventTypes.WorkflowShifted, Row("review", "review", "shift", outcome: null)),
            (EventTypes.WorkflowExpired, Row("review", "expired", "timer", "expired")),
            (EventTypes.WorkflowHeavy, Row("review", "review", "heavy", outcome: null)),
            (EventTypes.WorkflowInjection, Row("review", "review", "injection", outcome: null)),
            (EventTypes.WorkflowRetired, Row("review", "retired", "retire", "retired")),
            (EventTypes.WorkflowRetireDone, Row("retired", "retired", "retire.done", "retired")),
            (EventTypes.RecipeRegistered, "{\"id\":\"workflow.step\",\"version\":1,\"hash\":\"" + hash + "\"}"),
            (EventTypes.RecipeApproved, "{\"id\":\"workflow.step\",\"version\":1,\"hash\":\"" + hash + "\"}"),
            (EventTypes.RecipeRevoked, "{\"id\":\"workflow.step\",\"version\":1,\"hash\":\"" + hash + "\"}"),
            (EventTypes.RecipeRegistrationCompleted, "{\"registered\":1}"),
            ("line.phaseChanged", "{\"line\":\"L-1\",\"phase\":\"build\",\"to\":\"review\"}"),
            ("scope.frozen", "{\"packet\":\"P-1\",\"hash\":\"sha256:bbbb\"}"),
        ];

        await Append(EventTypes.ItemCreated, new ItemCreated(_h.Ids.New<CommandId>(), _item, "old work", Repo: null, Link: null, Body: null, Owner: null), "c1");
        var n = 0;
        foreach (var (type, json) in rows)
        {
            n++;
            await Append(type, Dummy(), "r" + n.ToString(CultureInfo.InvariantCulture), _ => json);
        }

        await Append(EventTypes.ItemLinked, new ItemLinked(_h.Ids.New<CommandId>(), _item, RecordKey.From("linear:issue/abc-123"), ItemAlias.From("ENG-142")), "k1");
        return rows.Length + 2;
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
