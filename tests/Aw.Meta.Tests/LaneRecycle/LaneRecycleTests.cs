using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneRecycle;

public sealed class LaneRecycleTests
{
    private const string Digest = "/home/x/digests/t1-api-2026-10-17.md";
    private readonly MetaWorld _world = new();

    // Waiting is when the lane's Claude "starts" its new session after /clear.
    private sealed class ClearTakesEffect(IDelay inner, Action onWait) : IDelay
    {
        public ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
        {
            onWait();
            return inner.Wait(duration, cancellationToken);
        }
    }

    private LaneRecycleService Service(Action? onWait = null)
    {
        var delay = onWait is null ? (IDelay)_world.Delay : new ClearTakesEffect(_world.Delay, onWait);
        return new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            new RecycleRunner(new PaneSubmitter(_world.Panes, delay), _world.LaneSessions, _world.LaneFiles, _world.Log, delay, _world.Clock));
    }

    private async Task Ready()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-old"), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-api", "s-old");
        _world.Panes.Put("t1-api", "claude");
        _world.ClaudeTranscripts.PutUsage("s-old", 487_000, A.At(30));
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));
    }

    private async Task<Kernel.Result<LaneRecycleResponse>> Run(Action? onWait = null, bool dryRun = false, string lane = "api") =>
        await Service(onWait).Handle(new LaneRecycleRequest(lane, Digest, dryRun), CancellationToken.None);

    private void NewSessionAppears() => _world.LaneSessions.Set("t1-api", "s-new");

    [Fact, Trait("Lane", "unit")]
    public async Task A_recycle_clears_waits_for_the_new_session_records_it_continues_from_the_digest_and_logs_a_note()
    {
        await Ready();

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal([("t1-api", "/clear"), ("t1-api", $"Read and continue from {Digest}")], _world.Panes.Woken);
        Assert.Equal(("s-old", "s-new", 487_000L), (result.Value.OldSession, result.Value.NewSession, result.Value.OldTokens));
        Assert.Equal("s-new", (await _world.LaneFiles.Record("api", CancellationToken.None)).Value!.Session!.Value.Value);
        var note = Assert.Single(_world.Log.Lines);
        Assert.StartsWith("- NOTE ", note, StringComparison.Ordinal);
        Assert.Contains("s-old -> s-new", note, StringComparison.Ordinal);
        Assert.Contains("487k", note, StringComparison.Ordinal);
        Assert.Contains(Digest, note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_enter_the_pane_swallows_is_pressed_again()
    {
        await Ready();
        _world.Panes.SwallowEnters = 1;

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, _world.Panes.Enters);
        Assert.Equal(2, _world.Panes.Woken.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_enter_that_never_gets_through_and_no_new_session_is_AW_META_012_with_nothing_recorded_or_sent()
    {
        await Ready();
        _world.Panes.SwallowEnters = 99;

        var result = await Run();

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Empty(_world.Panes.Woken);
        Assert.Equal("s-old", (await _world.LaneFiles.Record("api", CancellationToken.None)).Value!.Session!.Value.Value);
        Assert.Empty(_world.Log.Lines);
    }

    // #1901: on a 14-column pane the screen after /clear still reads as "/clear" in the input box, though Claude did clear.
    [Fact, Trait("Lane", "unit")]
    public async Task A_clear_the_pane_misreads_is_known_by_the_new_session_id_and_the_recycle_goes_on()
    {
        await Ready();
        var rule = new string('─', 14);
        _world.Panes.Show("t1-api", $"{rule}\n❯ /clear\n{rule}\n");

        var result = await Run(NewSessionAppears);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal([("t1-api", "/clear"), ("t1-api", $"Read and continue from {Digest}")], _world.Panes.Woken);
        Assert.Equal("s-new", (await _world.LaneFiles.Record("api", CancellationToken.None)).Value!.Session!.Value.Value);
        Assert.Contains("s-old -> s-new", Assert.Single(_world.Log.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_session_that_never_appears_is_AW_META_022_and_the_continue_line_is_not_sent()
    {
        await Ready();

        var result = await Run();

        Assert.Equal(MetaErrors.RecycleTimedOut.Code, result.Error.Code);
        Assert.Equal(
            "lane api did not report a new session id within 180 s of /clear; nothing more was sent. Once it is up, send the line by hand: "
            + "tmux -L t1 send-keys -t '=t1-api:' -l 'Read and continue from /home/x/digests/t1-api-2026-10-17.md' && tmux -L t1 send-keys -t '=t1-api:' Enter",
            result.Error.Message);
        Assert.Equal([("t1-api", "/clear")], _world.Panes.Woken);
        Assert.Equal("s-old", (await _world.LaneFiles.Record("api", CancellationToken.None)).Value!.Session!.Value.Value);
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_empty_or_old_digest_is_refused_before_anything_is_typed()
    {
        await Ready();
        _world.Digests.Put("/e.md", 0, _world.Clock.GetUtcNow());
        _world.Digests.Put("/old.md", 10, _world.Clock.GetUtcNow().AddHours(-3));

        foreach (var path in new[] { "/missing.md", "/e.md", "/old.md" })
        {
            var result = await Service().Handle(new LaneRecycleRequest("api", path, DryRun: false), CancellationToken.None);
            Assert.Equal(MetaErrors.RecycleRefused.Code, result.Error.Code);
        }

        Assert.Empty(_world.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_non_claude_lane_and_an_unknown_lane_are_refused()
    {
        await Ready();
        _ = _world.PutLane("ui", "codex");

        Assert.Equal(MetaErrors.RecycleRefused.Code, (await Run(lane: "ui")).Error.Code);
        Assert.Equal(MetaErrors.UnknownLane.Code, (await Run(lane: "meta")).Error.Code);
        Assert.Empty(_world.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dry_run_lists_the_steps_and_sends_nothing()
    {
        await Ready();

        var result = await Run(dryRun: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.DryRun);
        Assert.Equal(5, result.Value.Steps.Count);
        Assert.Empty(_world.Panes.Woken);
        Assert.Empty(_world.Log.Lines);
        Assert.Equal(487_000L, result.Value.OldTokens);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_digest_exactly_two_hours_old_is_accepted_and_one_second_older_is_refused()
    {
        await Ready();
        _world.Digests.Put("/at.md", 10, _world.Clock.GetUtcNow().AddHours(-2));
        _world.Digests.Put("/past.md", 10, _world.Clock.GetUtcNow().AddHours(-2).AddSeconds(-1));

        var accepted = await Service().Handle(new LaneRecycleRequest("api", "/at.md", DryRun: true), CancellationToken.None);
        var refused = await Service().Handle(new LaneRecycleRequest("api", "/past.md", DryRun: true), CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(MetaErrors.RecycleRefused.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_live_session_the_recycle_is_refused_and_the_stale_lane_sessions_id_is_never_the_old_one()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s-stale"), Agent: null, A.At(1)));
        _world.Panes.Put("t1-api", "claude");
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));

        var result = await Run(NewSessionAppears);

        Assert.Equal(MetaErrors.RecycleRefused.Code, result.Error.Code);
        Assert.Contains("no live Claude session", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_world.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relative_digest_becomes_absolute_before_it_is_checked_and_typed()
    {
        await Ready();
        var absolute = Path.GetFullPath("digest-rel.md");
        _world.Digests.Put(absolute, 10, _world.Clock.GetUtcNow());

        var result = await Service(NewSessionAppears).Handle(new LaneRecycleRequest("api", "digest-rel.md", DryRun: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(absolute, result.Value.Digest);
        Assert.Equal($"Read and continue from {absolute}", _world.Panes.Woken[^1].Line);
    }
}
