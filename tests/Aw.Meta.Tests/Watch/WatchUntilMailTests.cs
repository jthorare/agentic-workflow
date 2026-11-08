using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Usage;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

/// <summary>#2122: a Codex or Cursor meta polls in the foreground; --until-mail ends the watch on the first poll where mail is waiting.</summary>
public sealed class WatchUntilMailTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(120);
    private readonly MetaWorld _world = new();
    private readonly List<string> _lines = [];

    private WatchService Service() => new(
        _world.Config,
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, new WatchSweeper(_world.Config, _world.Sweeper, _world.Values), Worlds.Reservation(_world), _world.Context),
        _world.Values,
        _world.Delay,
        _world.Clock,
        new UsageMonitor(new UsageReader(_world.LaneFiles, _world.LaneSessions, _world.ClaudeTranscripts, _world.Panes, _world.Messages, _world.Clock), new UsageWatcher(new PaneSubmitter(_world.Panes, _world.Delay), _world.Log, _world.Values, _world.Clock), new Aw.Meta.Core.Routing.RouteWatcher(new Aw.Meta.Core.Routing.LaneStandingReader(_world.LaneFiles, _world.CodexRollouts, _world.Clock), _world.Values)));

    // Mail arrives while poll number `mailAtPoll` (from 0) runs, so that poll sees it; -1 sends none.
    private void Script(int mailAtPoll)
    {
        var polls = -1;
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                polls++;
                if (polls == mailAtPoll)
                {
                    _world.Receive("m1", "t1-api", "can you look at this").GetAwaiter().GetResult();
                }

                return new CommandResult(0, "[]", string.Empty, TimedOut: false);
            })
            .When("git ls-remote origin main", _ => new CommandResult(0, "aaaaaaa1111111\trefs/heads/main\n", string.Empty, TimedOut: false));
    }

    // The message arrives with poll `mailAtPoll` and is dismissed at once, so the inbox never holds an open one.
    private void ScriptResolved()
    {
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                _world.Receive("m1", "t1-api", "fyi only").GetAwaiter().GetResult();
                var resolved = _world.Messages.Resolve(MessageId.From("m1"), new MessageResolution(MessageResolutionKind.Dismissed, "fyi"), CancellationToken.None).AsTask().GetAwaiter().GetResult();
                Assert.True(resolved.IsSuccess);
                return new CommandResult(0, "[]", string.Empty, TimedOut: false);
            })
            .When("git ls-remote origin main", _ => new CommandResult(0, "aaaaaaa1111111\trefs/heads/main\n", string.Empty, TimedOut: false));
    }

    private async Task<WatchResponse> Watch(bool untilMail)
    {
        await _world.Ready();
        var request = new WatchRequest(_interval, TimeSpan.FromMinutes(10), line => { _lines.Add(line); return ValueTask.CompletedTask; }) { UntilMail = untilMail };
        var result = await Service().Handle(request, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mail_waiting_at_the_first_poll_ends_the_watch_after_one_poll()
    {
        Script(mailAtPoll: 0);

        var response = await Watch(untilMail: true);

        Assert.Equal((1, true), (response.Polls, response.EndedOnMail));
        Assert.Contains("INBOX 1 open message(s)", _lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mail_at_the_third_poll_ends_the_watch_after_three_polls()
    {
        Script(mailAtPoll: 2);

        var response = await Watch(untilMail: true);

        Assert.Equal((3, true), (response.Polls, response.EndedOnMail));
        Assert.Contains("INBOX 1 open message(s)", _lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_mail_runs_to_the_end_of_the_watch()
    {
        Script(mailAtPoll: -1);

        var response = await Watch(untilMail: true);

        Assert.False(response.EndedOnMail);
        Assert.True(response.Polls > 3);
        Assert.Contains("WATCH EXPIRING: rerun aw-meta watch", _lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_the_flag_mail_does_not_end_the_watch()
    {
        Script(mailAtPoll: 0);

        var response = await Watch(untilMail: false);

        Assert.False(response.EndedOnMail);
        Assert.True(response.Polls > 1);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mail_that_is_already_resolved_never_ends_the_watch()
    {
        ScriptResolved();

        var response = await Watch(untilMail: true);

        Assert.False(response.EndedOnMail);
        Assert.DoesNotContain(_lines, l => l.StartsWith("INBOX", StringComparison.Ordinal) && l.Contains("open", StringComparison.Ordinal));
    }
}
