using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ListOutbox;
using Aw.Sindri.Host.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ListOutbox;

/// <summary>
/// 'sindri outbox list [--pending | --dispatched] [--json]' (engine spec §6.3; engine P6 unit (c)): the effects, read from
/// their ledger rows (§5.6, §5.7) and folded by command, the latest row winning; one line each, or 'no commands'.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ListOutboxTests
{
    private static readonly InstanceAddress _t4 = InstanceAddress.From("T-4/dev-notify/1");
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_effect_shows_at_its_latest_row()
    {
        var h = await Ledger();

        var all = (await new ListOutboxService(h.Store, h.Events).Handle(new ListOutboxRequest(), Token)).Value.Commands;

        Assert.Equal(["c-recipe", "c-engine", "c-revoked"], all.Select(static c => c.Command), StringComparer.Ordinal);
        Assert.Equal([EffectState.Dispatched, EffectState.Pending, EffectState.Refused], all.Select(static c => c.State));
        Assert.Equal([4L, 3L, 6L], all.Select(static c => c.Sequence));
        Assert.Equal((CommandOrigin.Recipe, _notify, _t4, "WFI-a:2:0"), (all[0].Origin, all[0].Kind, all[0].Instance, all[0].Identity));
        Assert.Equal("SND-RECIPE-005", all[2].Code);
    }

    [Fact]
    public async Task An_effect_cancelled_within_its_delay_shows_cancelled_and_is_neither_pending_nor_dispatched()
    {
        var h = await Ledger();
        var grant = h.Held.Current!;   // why: Ledger() holds the lease
        var t6 = InstanceAddress.From("T-6/dev-notify/1");
        foreach (var (type, state, key) in new[] { (OutboxEvents.Pending, EffectState.Pending, "workflow:T-6:run:2"), (OutboxEvents.Cancelled, EffectState.Cancelled, "workflow:T-6:run:3") })
        {
            var row = new EffectRow("WFI-c:2:0", "c-cancelled", _notify, CommandOrigin.Recipe, state, t6);
            Assert.False((await h.Store.Append(h.Events.Entry(type, row, key, "T-6"), grant, Token)).IsError);
        }

        var service = new ListOutboxService(h.Store, h.Events);
        var all = (await service.Handle(new ListOutboxRequest(), Token)).Value.Commands;
        var pending = (await service.Handle(new ListOutboxRequest(EffectState.Pending), Token)).Value.Commands;
        var dispatched = (await service.Handle(new ListOutboxRequest(EffectState.Dispatched), Token)).Value.Commands;

        var cancelled = Assert.Single(all, static c => c.Command == "c-cancelled");
        Assert.Equal(EffectState.Cancelled, cancelled.State);
        Assert.Contains("  cancelled  effect.notify.desk  recipe  T-6/dev-notify/1  WFI-c:2:0", OutboxCommandTable.Lines([cancelled]), StringComparison.Ordinal);
        Assert.DoesNotContain(pending.Concat(dispatched), static c => c.Command == "c-cancelled");
    }

    [Fact]
    public async Task Pending_and_dispatched_keep_the_effects_in_that_state()
    {
        var h = await Ledger();
        var service = new ListOutboxService(h.Store, h.Events);

        var pending = (await service.Handle(new ListOutboxRequest(EffectState.Pending), Token)).Value.Commands;
        var dispatched = (await service.Handle(new ListOutboxRequest(EffectState.Dispatched), Token)).Value.Commands;

        Assert.Equal(["c-engine"], pending.Select(static c => c.Command), StringComparer.Ordinal);
        Assert.Equal(["c-recipe"], dispatched.Select(static c => c.Command), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_lines_name_each_effect_and_an_empty_list_says_no_commands()
    {
        var h = await Ledger();
        var all = (await new ListOutboxService(h.Store, h.Events).Handle(new ListOutboxRequest(), Token)).Value.Commands;
        var at = all[0].At.UtcDateTime.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var lines = OutboxCommandTable.Lines(all).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal($"4  {at}  dispatched  effect.notify.desk  recipe  T-4/dev-notify/1  WFI-a:2:0", lines[0]);
        Assert.Equal($"3  {at}  pending  effect.notify.desk  engine  T-4/dev-notify/1  WFI-a:1:n1", lines[1]);
        Assert.Equal($"6  {at}  refused (SND-RECIPE-005)  effect.notify.desk  recipe  T-5/dev-notify/1  WFI-b:2:0", lines[2]);
        Assert.Equal("no commands\n", OutboxCommandTable.Lines([]));
    }

    [Fact]
    public async Task The_verb_prints_lines_or_json_and_the_empty_states()
    {
        var h = await Ledger();
        var empty = new SliceHarness();

        var (code, pending) = await Run(h, "list", "--pending");
        var (_, json) = await Run(h, "list", "--dispatched", "--json");
        var (_, none) = await Run(empty, "list", "--dispatched");
        var (bad, _) = await Run(h, "list", "--refused");

        Assert.Equal(0, code);
        Assert.Contains("pending  effect.notify.desk  engine", pending, StringComparison.Ordinal);
        Assert.DoesNotContain("dispatched", pending, StringComparison.Ordinal);
        var commands = JsonDocument.Parse(json).RootElement.GetProperty("commands");
        Assert.Equal("dispatched", Assert.Single(commands.EnumerateArray()).GetProperty("state").GetString());
        Assert.Equal("no commands\n", none);
        Assert.Equal(2, bad);
    }

    private static async Task<(int Code, string Stdout)> Run(SliceHarness h, params string[] args)
    {
        await using var services = new ServiceCollection()
            .AddSingleton<IService<ListOutboxRequest, ListOutboxResponse>>(new ListOutboxService(h.Store, h.Events))
            .BuildServiceProvider();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = await OutboxCommandTable.Run(args, services, new Output(stdout, stderr, SliceHarness.Json), Token);
        return (code, stdout.ToString());
    }

    /// <summary>
    /// Six rows: a heartbeat; T-4's recipe effect pending (2); the engine's first-task notice pending (3); the recipe
    /// effect dispatched (4); T-5's recipe effect pending (5), then refused by the revoke (6).
    /// </summary>
    private static async Task<SliceHarness> Ledger()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var t5 = InstanceAddress.From("T-5/dev-notify/1");
        (string Type, string Key, string Item, EffectRow? Row)[] rows =
        [
            ("service.heartbeat", "service.heartbeat:1:1", "T-0", null),
            (OutboxEvents.Pending, "workflow:T-4:run:2", "T-4", new("WFI-a:2:0", "c-recipe", _notify, CommandOrigin.Recipe, EffectState.Pending, _t4)),
            (OutboxEvents.Pending, "workflow:T-4:run:3", "T-4", new("WFI-a:1:n1", "c-engine", _notify, CommandOrigin.Engine, EffectState.Pending, _t4)),
            (OutboxEvents.Dispatched, "effect:WFI-a:2:0:dispatched", "T-4", new("WFI-a:2:0", "c-recipe", _notify, CommandOrigin.Recipe, EffectState.Dispatched, _t4)),
            (OutboxEvents.Pending, "workflow:T-5:run:2", "T-5", new("WFI-b:2:0", "c-revoked", _notify, CommandOrigin.Recipe, EffectState.Pending, t5)),
            (OutboxEvents.Refused, "workflow:T-5:run:4", "T-5", new("WFI-b:2:0", "c-revoked", _notify, CommandOrigin.Recipe, EffectState.Refused, t5, "SND-RECIPE-005", "revoked")),
        ];
        foreach (var (type, key, item, row) in rows)
        {
            var entry = row is null ? h.Events.Entry(type, new Heartbeat(KnownInstants.Epoch, KnownInstants.Epoch), key) : h.Events.Entry(type, row, key, item);
            Assert.False((await h.Store.Append(entry, grant, Token)).IsError);
        }

        return h;
    }
}
