using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

/// <summary>#2564: two watches on one team share watch.snapshot. The newest watch owns it; an older one prints WATCH superseded and ends without writing.</summary>
public sealed class WatchSupersededTests : IDisposable
{
    private const int Older = 100;
    private const int Newer = 200;
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(120);
    private readonly MetaWorld _world = new();
    private readonly List<string> _lines = [];

    // Ends any watch still polling after 20 polls, so a watch that is never superseded fails instead of hanging.
    private readonly CancellationTokenSource _cap = new();

    private WatchService Service() => new(
        _world.Config,
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, new WatchSweeper(_world.Config, _world.Sweeper, _world.Values), Worlds.Reservation(_world), _world.Context),
        _world.Values,
        _world.Delay,
        _world.Clock,
        _world.Monitor,
        _world.Board);

    private WatchRequest Request(int owner, bool untilEvent) =>
        new(_interval, TimeSpan.FromMinutes(10), line => { _lines.Add(line); return ValueTask.CompletedTask; }) { UntilEvent = untilEvent, Owner = owner };

    // Every poll sees no pull request and the same main; `onPoll` runs inside poll n (from 0), before it settles.
    private void Script(Action<int> onPoll)
    {
        var polls = -1;
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                polls++;
                if (polls == 20)
                {
                    _cap.Cancel();
                }

                onPoll(polls);
                return new CommandResult(0, "[]", string.Empty, TimedOut: false);
            })
            .When("git ls-remote origin main", _ => new CommandResult(0, "aaaaaaa1111111\trefs/heads/main\n", string.Empty, TimedOut: false));
    }

    private string? Read(string key) => _world.Values.Get(key, CancellationToken.None).AsTask().Result.Value;

    private void Write(string key, string value) => _ = _world.Values.Set(key, value, CancellationToken.None).AsTask().Result;

    public void Dispose() => _cap.Dispose();

    [Theory, Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_watch_claims_watch_owner_when_it_starts(bool untilEvent)
    {
        Script(n =>
        {
            if (n == 2)
            {
                _cap.Cancel();
            }
        });
        await _world.Ready();
        Write(KvKeys.WatchOwner, "42");

        _ = await Record.ExceptionAsync(async () => await Service().Handle(Request(Older, untilEvent), _cap.Token));

        Assert.Equal("100", Read(KvKeys.WatchOwner));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_newer_watch_saving_mid_poll_ends_the_older_one_and_keeps_the_newer_snapshot(bool untilEvent)
    {
        // Poll 1 of the older watch: a newer watch starts and saves its own baseline before the older one settles.
        Script(n =>
        {
            if (n == 1)
            {
                Write(KvKeys.WatchOwner, "200");
                Write(KvKeys.WatchSnapshot, "newer-baseline");
            }
        });
        await _world.Ready();

        var result = await Service().Handle(Request(Older, untilEvent), _cap.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(2, result.Value.Polls);
        Assert.Equal("WATCH superseded by pid 200: a newer watch runs, do not restart", _lines[^1]);
        Assert.Equal("newer-baseline", Read(KvKeys.WatchSnapshot));
        Assert.Equal("200", Read(KvKeys.WatchOwner));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_newer_watch_that_only_claimed_ends_the_older_one_before_it_saves(bool untilEvent)
    {
        string? before = null;
        Script(n =>
        {
            if (n == 1)
            {
                before = Read(KvKeys.WatchSnapshot);
                Write(KvKeys.WatchOwner, "200");
            }
        });
        await _world.Ready();

        var result = await Service().Handle(Request(Older, untilEvent), _cap.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal("WATCH superseded by pid 200: a newer watch runs, do not restart", _lines[^1]);
        Assert.NotNull(before);
        Assert.Equal(before, Read(KvKeys.WatchSnapshot));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_watch_that_still_owns_watch_owner_keeps_saving()
    {
        Script(_ => { });
        await _world.Ready();

        var result = await Service().Handle(Request(Newer, untilEvent: false), _cap.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("WATCH EXPIRING: rerun aw-meta watch", _lines[^1]);
        Assert.DoesNotContain(_lines, l => l.StartsWith("WATCH superseded", StringComparison.Ordinal));
        Assert.NotNull(Read(KvKeys.WatchSnapshot));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_superseded_watch_whose_poll_fails_never_writes_watch_failing(bool untilEvent)
    {
        // Poll 1 fails, and a newer watch claims inside it; the older watch must not start a failing streak for the team.
        var polls = -1;
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                polls++;
                if (polls == 20)
                {
                    _cap.Cancel();
                }

                if (polls == 1)
                {
                    Write(KvKeys.WatchOwner, "200");
                    return new CommandResult(1, string.Empty, "network down", TimedOut: false);
                }

                return new CommandResult(0, "[]", string.Empty, TimedOut: false);
            })
            .When("git ls-remote origin main", _ => new CommandResult(0, "aaaaaaa1111111\trefs/heads/main\n", string.Empty, TimedOut: false));
        await _world.Ready();
        Write(KvKeys.WatchFailing, "0");

        var result = await Service().Handle(Request(Older, untilEvent), _cap.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal("WATCH superseded by pid 200: a newer watch runs, do not restart", _lines[^1]);
        Assert.DoesNotContain(_lines, l => l.StartsWith("WATCH ERROR", StringComparison.Ordinal));
        Assert.Equal("0", Read(KvKeys.WatchFailing));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_older_watch_writing_before_it_sees_the_claim_never_ends_the_newest_one()
    {
        // Poll 1 of the newest watch: an older watch that has not yet seen the claim saves its own baseline. The newest watch still owns
        // watch.owner, so it overwrites that baseline and keeps polling to the end of its window.
        Script(n =>
        {
            if (n == 1)
            {
                Write(KvKeys.WatchSnapshot, "older-baseline");
            }
        });
        await _world.Ready();

        var result = await Service().Handle(Request(Newer, untilEvent: false), _cap.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.DoesNotContain(_lines, l => l.StartsWith("WATCH superseded", StringComparison.Ordinal));
        Assert.Equal("WATCH EXPIRING: rerun aw-meta watch", _lines[^1]);
        Assert.NotEqual("older-baseline", Read(KvKeys.WatchSnapshot), StringComparer.Ordinal);
        Assert.Equal("200", Read(KvKeys.WatchOwner));
    }
}
