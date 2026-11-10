using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class OwnerLanesAndStatusCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private string Transcript => _cli.File("meta.jsonl");

    private async Task Ready()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "api,ui", "--log", _cli.File("log.md"));
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "t1-meta");
    }

    private static string Hook(string to, string message) =>
        $$$"""{"session_id":"s-meta","tool_name":"SendMessage","tool_input":{"to":"{{{to}}}","message":"{{{message}}}"}}""";

    [Fact, Trait("Lane", "integration")]
    public async Task An_owner_question_is_answered_logged_and_relayed_before_the_stop_is_released()
    {
        await Ready();
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "t1-api", "OWNER QUESTION: sqlite or postgres?"), TestContext.Current.CancellationToken);
        var sync = await _cli.Run("inbox-sync", "--session", "s-meta", "--transcript", Transcript);
        var queue = await _cli.Run("owner");
        _cli.Clock.Advance(TimeSpan.FromMinutes(1));
        var ack = await _cli.RunWithInput(Hook("t1-api", "queued for the owner"), "outbox-record");

        var answered = await _cli.Run("owner-answer", "q1", "--answer", "sqlite");
        var blocked = await _cli.Run("stop-check", "--session", "s-meta", "--transcript", Transcript);
        _cli.Clock.Advance(TimeSpan.FromMinutes(1));
        var relayed = await _cli.RunWithInput(Hook("t1-api", "OWNER ANSWER q1: sqlite"), "outbox-record");
        var released = await _cli.Run("stop-check", "--session", "s-meta", "--transcript", Transcript);

        Assert.Contains("1 new message(s): m1", sync.Out, StringComparison.Ordinal);
        Assert.Contains("q1  t1-api  sqlite or postgres?", queue.Out, StringComparison.Ordinal);
        Assert.Contains("closed: reply to t1-api (m1)", ack.Out, StringComparison.Ordinal);
        Assert.Contains("- OWNER (2026-10-15): sqlite (asked by t1-api)", answered.Out, StringComparison.Ordinal);
        Assert.Equal(1, blocked.Exit);
        Assert.Contains("next: SendMessage \"OWNER ANSWER q1: sqlite\" to t1-api", blocked.Out, StringComparison.Ordinal);
        Assert.Contains("closed: owner answer q1", relayed.Out, StringComparison.Ordinal);
        Assert.Equal(0, released.Exit);
        Assert.Contains("- OWNER (2026-10-15): sqlite (asked by t1-api)", await File.ReadAllTextAsync(_cli.File("log.md"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_lane_map_is_announced_and_owes_nothing_because_lanes_read_it_from_the_board()
    {
        await Ready();
        await File.WriteAllTextAsync(Path.Combine(_cli.SessionsDirectory, "1.json"), "{\"pid\":" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"sessionId\":\"s-api\",\"name\":\"t1-api\",\"status\":\"idle\",\"cwd\":\"/w\"}", TestContext.Current.CancellationToken);
        var refresh = await _cli.Run("lanes-refresh");
        var before = await _cli.Run("status");
        var announce = await _cli.Run("lanes", "--announce");
        var table = await _cli.Run("lanes");

        Assert.Contains("(changed)", refresh.Out, StringComparison.Ordinal);
        Assert.StartsWith("LANE MAP v1: api=t1-api", announce.Out, StringComparison.Ordinal);
        Assert.Equal(0, before.Exit);   // a new lane map version owes nothing: lanes see it through board-line
        Assert.DoesNotContain("[BroadcastLanes]", before.Out, StringComparison.Ordinal);
        Assert.Contains("lanes: v1 api=t1-api", before.Out, StringComparison.Ordinal);
        Assert.Contains("ui           (no session)", table.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Log_append_writes_the_line_to_the_configured_log_and_a_bad_kind_is_a_usage_error()
    {
        await Ready();

        var ok = await _cli.Run("log-append", "--kind", "note", "--text", "hello there");
        var bad = await _cli.Run("log-append", "--kind", "shout", "--text", "x");

        Assert.Equal("- NOTE 2026-10-15 05:00: hello there", ok.Out.Trim());
        Assert.Equal("- NOTE 2026-10-15 05:00: hello there", (await File.ReadAllTextAsync(_cli.File("log.md"), TestContext.Current.CancellationToken)).Trim());
        Assert.Equal(2, bad.Exit);
        Assert.StartsWith("AW-META-001: ", bad.Err, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_meta_s_own_question_is_queued_and_answering_an_unknown_one_is_AW_META_006()
    {
        await Ready();

        var asked = await _cli.Run("owner-ask", "--text", "merge #190 first?");
        var missing = await _cli.Run("owner-answer", "9", "--answer", "x");

        Assert.Contains("queued as q1", asked.Out, StringComparison.Ordinal);
        Assert.Equal((2, "AW-META-006"), (missing.Exit, missing.Err[..11]));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Queue_drop_removes_the_entry_logs_the_reason_and_an_empty_queue_is_AW_META_018()
    {
        await Ready();
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "t1-api", "ready #250 079c2541"), TestContext.Current.CancellationToken);
        _ = await _cli.Run("inbox-sync", "--session", "s-meta", "--transcript", Transcript);

        var before = await _cli.Run("status");
        var dropped = await _cli.Run("queue-drop", "250", "--reason", "not a ready");
        var after = await _cli.Run("status");
        var again = await _cli.Run("queue-drop", "#250", "--reason", "x");

        Assert.Contains("#250 ready", before.Out, StringComparison.Ordinal);
        Assert.Equal(0, dropped.Exit);
        Assert.Contains("merge queue: empty", after.Out, StringComparison.Ordinal);
        Assert.Contains("queue-drop #250: not a ready", await File.ReadAllTextAsync(_cli.File("log.md"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal((2, "AW-META-018"), (again.Exit, again.Err[..11]));
    }

    public void Dispose() => _cli.Dispose();
}
