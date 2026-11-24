using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Watch;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

/// <summary>#3735: a transient network error of gh ("unexpected EOF", "TLS handshake timeout") ended `watch --until-event` with AW-META-012, because the WATCH ERROR line counts as an event. Transient errors are retried with a short backoff; others still end the watch at once.</summary>
public sealed class WatchRetryTests
{
    private readonly MetaWorld _world = new();
    private readonly List<string> _lines = [];

    private WatchService Service() => new(
        _world.Config,
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, Worlds.FollowUps(_world), Worlds.Reservation(_world), _world.Context),
        _world.Values,
        _world.Delay,
        _world.Clock,
        new LaneReport(_world.Monitor, _world.Values),
        _world.Board);

    private WatchRequest Request() =>
        new(TimeSpan.FromSeconds(120), TimeSpan.FromMinutes(10), line => { _lines.Add(line); return ValueTask.CompletedTask; }) { UntilEvent = true };

    // gh pr list fails with `stderr` for the first `failures` calls, then lists nothing once (the watch's starting snapshot,
    // which prints no event), then lists pull request 190, so the next poll reports it opened.
    private void Script(int failures, string stderr)
    {
        var calls = 0;
        _ = _world.Runner
            .When("gh pr list", _ => ++calls switch
            {
                var n when n <= failures => new CommandResult(1, string.Empty, stderr, TimedOut: false),
                var n when n == failures + 1 => new CommandResult(0, "[]", string.Empty, TimedOut: false),
                _ => new CommandResult(0, """[{"number":190,"state":"OPEN","isDraft":false,"headRefOid":"aaa1111","mergeable":"MERGEABLE","title":"t190"}]""", string.Empty, TimedOut: false),
            })
            .When("git ls-remote origin main", "aaaaaaa1111111\trefs/heads/main\n");
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Post \"https://api.github.com/graphql\": net/http: TLS handshake timeout")]
    [InlineData("Post \"https://api.github.com/graphql\": unexpected EOF")]
    [InlineData("error connecting to api.github.com")]
    public async Task A_transient_network_error_is_retried_and_the_watch_still_reports_the_event(string stderr)
    {
        await _world.Ready();
        Script(failures: 2, stderr);

        var result = await Service().Handle(Request(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.DoesNotContain(_lines, l => l.StartsWith("WATCH ERROR", StringComparison.Ordinal));
        Assert.Contains(_lines, l => l.Contains("#190", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("HTTP 401: Bad credentials (https://api.github.com/graphql)")]
    [InlineData("unknown flag: --jsno")]
    public async Task A_non_network_error_still_ends_the_watch_at_once(string stderr)
    {
        await _world.Ready();
        Script(failures: 1, stderr);

        _ = await Service().Handle(Request(), CancellationToken.None);

        Assert.StartsWith("WATCH ERROR: AW-META-012", _lines[0], StringComparison.Ordinal);
        _ = Assert.Single(_world.Runner.Requests, r => string.Join(' ', r.Argv).Contains("gh pr list", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_network_error_that_never_clears_ends_the_watch_after_the_cap()
    {
        await _world.Ready();
        Script(failures: int.MaxValue, "net/http: TLS handshake timeout");

        _ = await Service().Handle(Request(), CancellationToken.None);

        Assert.StartsWith("WATCH ERROR: AW-META-012", _lines[0], StringComparison.Ordinal);
        Assert.Equal(TransientGhError.Retries + 1, _world.Runner.Requests.Count(r => string.Join(' ', r.Argv).Contains("gh pr list", StringComparison.Ordinal)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("gh pr list: Post \"https://api.github.com/graphql\": net/http: TLS handshake timeout", true)]
    [InlineData("gh pr list: unexpected EOF", true)]
    [InlineData("gh pr list: HTTP 502: Bad Gateway", true)]
    [InlineData("git ls-remote: fatal: unable to access 'https://github.com/x/y/': Could not resolve host: github.com", true)]
    [InlineData("gh pr list: HTTP 401: Bad credentials", false)]
    [InlineData("gh pr list: unknown flag: --jsno", false)]
    [InlineData("gh pr list: timed out", false)]
    public void The_classifier_reads_the_error_text(string message, bool transient) =>
        Assert.Equal(transient, TransientGhError.IsTransient(message));
}
