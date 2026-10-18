using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The item's launch block (UI spec E-2, launcher L-15): decided from projections only.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchOptionsTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));
    private static readonly SessionId _session = SessionId.From("0b1c2d3e-0000-4000-8000-000000000001");

    private static ItemRow Item(ItemState state = ItemState.Running, EngineItemPart? engine = null) =>
        new(_item, "Ship it", RepoName.From("web"), state, Step: null, Version: 1, "human:cli", _now, _now, _now, Link: null, Alias: null, Body: null, WaitingOn: null, Instance: null, Owner: null, Sequence: 1) { Engine = engine };

    private static EngineItemPart Engine(SuspensionKind? suspension = null) => new("WFI-1", "work", RunOpen: false, Runtime: null, suspension, Ended: false, Cancelled: false);

    private static LaunchRow Row(int n, LaunchState state, bool live = false, SessionId? reported = null, string? code = null, TimeSpan? age = null) =>
        new(LaunchId.From("LCH-" + n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), _item, RepoName.From("web"), LaunchMode.Open, AgentId.From("claude-code"), "click", state,
            _now - (age ?? TimeSpan.FromMinutes(5)), OutcomeAt: null, code, Message: null, Stage: null, SessionId: null, reported, TabTitle: "sindri " + _item.Value, Viewer: null, Late: false,
            Attach: state == LaunchState.Started ? ["tmux", "attach"] : null, CatalogSource: null, ArgvHash: null, live || state != LaunchState.Started ? null : new LaunchEnd(SessionEndReason.Exit, 0, _now), Sequence: n);

    private static ItemLaunch For(ItemRow item, params LaunchRow[] launches) => LaunchOptions.For(new LaunchOptionInputs
    {
        Item = item,
        Launches = launches,
        InCatalog = static _ => true,
        Now = _now,
        RepoPath = "/work/web",
    });

    [Fact]
    public void Attach_offered_only_when_has_session()
    {
        var none = For(Item());
        var live = For(Item(), Row(1, LaunchState.Started, live: true));
        var ended = For(Item(), Row(1, LaunchState.Started, live: false));

        Assert.Equal([LaunchMode.Open], none.Modes);
        Assert.Equal([LaunchMode.Open, LaunchMode.Attach], live.Modes);
        Assert.NotNull(live.Attach);
        Assert.Equal([LaunchMode.Open], ended.Modes);
        Assert.Null(ended.Attach);
    }

    [Fact]
    public void Resume_needs_handback_and_a_session_id()
    {
        var ended = Row(1, LaunchState.Started, live: false, reported: _session);

        Assert.DoesNotContain(LaunchMode.Resume, For(Item(), ended).Modes);                                   // no engine instance: nothing to hand back
        Assert.DoesNotContain(LaunchMode.Resume, For(Item(engine: Engine(SuspensionKind.Takeover)), ended).Modes);   // still taken over
        Assert.DoesNotContain(LaunchMode.Resume, For(Item(engine: Engine()), Row(1, LaunchState.Started, live: false)).Modes);   // no session id
        Assert.Contains(LaunchMode.Resume, For(Item(engine: Engine()), ended).Modes);
    }

    [Fact]
    public void Resumable_reads_session_reported_after_rebuild()
    {
        var launch = For(Item(engine: Engine()), Row(1, LaunchState.Started, live: false, reported: _session));

        Assert.Equal(_session, launch.Resumable?.SessionId);
    }

    [Fact]
    public void Confirm_set_within_window_and_after_007_009()
    {
        var started = For(Item(), Row(1, LaunchState.Started, live: false, age: TimeSpan.FromMinutes(30)));
        var interrupted = For(Item(), Row(1, LaunchState.Failed, code: SindriErrors.LaunchInterrupted.Code));
        var timedOut = For(Item(), Row(1, LaunchState.Failed, code: SindriErrors.LaunchTimedOut.Code));
        var refused = For(Item(), Row(1, LaunchState.Failed, code: SindriErrors.LaunchAgentMissing.Code));
        var old = For(Item(), Row(1, LaunchState.Started, live: false, age: TimeSpan.FromHours(3)));
        var takenOver = For(Item(engine: Engine(SuspensionKind.Takeover)), Row(1, LaunchState.Started, live: false));

        Assert.NotNull(started.Confirm);
        Assert.NotNull(interrupted.Confirm);
        Assert.NotNull(timedOut.Confirm);
        Assert.Null(refused.Confirm);
        Assert.Null(old.Confirm);
        Assert.Null(takenOver.Confirm);
    }

    [Fact]
    public void A_closed_item_offers_nothing()
    {
        var closed = For(Item(ItemState.Done), Row(1, LaunchState.Started, live: true));

        Assert.Empty(closed.Modes);
        Assert.Empty(closed.Blockers);
    }

    [Fact]
    public void Default_target_follows_last_used_then_profile_then_builtin()
    {
        var builtIn = For(Item());
        var lastUsed = LaunchOptions.For(new LaunchOptionInputs { Item = Item(), Launches = [], InCatalog = static _ => true, Now = _now, LastUsedInRepo = AgentId.From("codex") });

        Assert.Equal(("claude-code", "builtIn"), (builtIn.DefaultTarget.Value, builtIn.ChosenBy));
        Assert.Equal(("codex", "lastUsed"), (lastUsed.DefaultTarget.Value, lastUsed.ChosenBy));
    }

    [Fact]
    public void A_repo_without_a_path_blocks_an_open()
    {
        var launch = LaunchOptions.For(new LaunchOptionInputs { Item = Item(), Launches = [], InCatalog = static _ => true, Now = _now });

        Assert.Equal([SindriErrors.LaunchRepoMissing.Code], launch.Blockers.Select(static b => b.Code), StringComparer.Ordinal);
    }
}
