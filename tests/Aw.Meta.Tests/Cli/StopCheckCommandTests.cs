using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class StopCheckCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private string Transcript => _cli.File("meta.jsonl");

    private async Task Ready()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "api,ui");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "t1-meta");
    }

    private Task<CliResult> Stop(string session = "s-meta", params string[] extra) =>
        _cli.Run(["stop-check", "--session", session, "--transcript", Transcript, .. extra]);

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_that_is_not_the_meta_stops_silently_even_with_no_team_config()
    {
        var result = await Stop("s-other");

        Assert.Equal((0, string.Empty, string.Empty), (result.Exit, result.Out, result.Err));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_meta_is_blocked_with_exit_1_and_the_next_command_while_a_peer_is_unanswered()
    {
        await Ready();
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"), TestContext.Current.CancellationToken);

        var blocked = await Stop();

        Assert.Equal(1, blocked.Exit);
        Assert.Contains("aw-meta: 2 open obligation(s)", blocked.Out, StringComparison.Ordinal);
        Assert.Contains("next: SendMessage to t1-api; or aw-meta inbox-dismiss m1", blocked.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Dismissing_the_message_releases_a_blocked_stop_and_deferring_blocks_it_again_when_due()
    {
        await Ready();
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"), TestContext.Current.CancellationToken);
        _ = await _cli.Run("inbox-sync", "--session", "s-meta", "--transcript", Transcript);
        var listed = await _cli.Run("inbox");

        var dismissed = await _cli.Run("inbox-dismiss", "m1", "--reason", "fyi");
        var released = await Stop();
        var deferred = await _cli.Run("inbox-defer", "m1", "--reason", "later", "--until", "+30m");
        var snoozed = await Stop();
        _cli.Clock.Advance(TimeSpan.FromMinutes(31));
        var due = await Stop();
        var all = await _cli.Run("inbox", "--all", "--json");

        Assert.Contains("m1  t1-api  Request  open  please rebase", listed.Out, StringComparison.Ordinal);
        Assert.Equal(0, dismissed.Exit);
        Assert.Equal((0, string.Empty), (released.Exit, released.Out));
        Assert.Equal((0, 0, 1), (deferred.Exit, snoozed.Exit, due.Exit));
        Assert.Contains("\"state\":\"open\"", all.Out.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_loop_guard_limit_comes_from_AW_META_STOP_MAX_and_a_continuation_is_let_through_with_a_warning()
    {
        await Ready();
        _cli.SetEnv("AW_META_STOP_MAX", "2");
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "t1-api", "stuck"), TestContext.Current.CancellationToken);

        var first = await Stop(extra: "--hook-active");
        var second = await Stop(extra: "--hook-active");
        var third = await Stop(extra: "--hook-active");

        Assert.Equal([1, 1, 0], [first.Exit, second.Exit, third.Exit]);
        Assert.StartsWith("aw-meta: stop allowed after 2 identical blocks", third.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_output_names_the_obligation_kind_as_a_string()
    {
        await Ready();
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "t1-api", "x"), TestContext.Current.CancellationToken);

        var result = await Stop(extra: "--json");

        Assert.Equal(1, result.Exit);
        Assert.Contains("\"kind\":\"Reply\"", result.Out.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_status_tagged_line_opens_no_obligation_and_a_tagged_ready_still_does()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "api,ideas");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "t1-meta");
        await File.WriteAllTextAsync(Transcript, MetaWorld.PeerLine("m1", "t1-ideas", "STATUS: review clean on #123"), TestContext.Current.CancellationToken);

        var quiet = await Stop();
        await File.AppendAllTextAsync(Transcript, MetaWorld.PeerLine("m2", "t1-ideas", "STATUS: ready #123 079c2541"), TestContext.Current.CancellationToken);
        var ready = await Stop();
        var status = await _cli.Run("inbox", "--status");
        var again = await _cli.Run("inbox", "--status");

        Assert.Equal((0, string.Empty), (quiet.Exit, quiet.Out));
        Assert.Equal(1, ready.Exit);
        Assert.Contains("reply to t1-ideas (m2)", ready.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("(m1)", ready.Out, StringComparison.Ordinal);
        Assert.Equal(0, status.Exit);
        Assert.Contains("m1  t1-ideas", status.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("m2", status.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("m1", again.Out, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
