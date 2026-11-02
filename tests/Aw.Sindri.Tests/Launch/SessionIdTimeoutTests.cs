using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.SessionExit;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A session id pattern that runs out of time is its own outcome, so a valid id is never refused as a mismatch on a loaded box.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionIdTimeoutTests
{
    // Building this pattern's automaton for a 128-character id takes longer than a millisecond, so a 1 ms limit times out every time.
    private const string Slow = "(a|b|c)*(a|b|d)*(a|b|e){1,1000}z";
    private static readonly string _id = new('a', 128);
    private static readonly TimeSpan _tight = TimeSpan.FromMilliseconds(1);
    private readonly SessionHarness _s = new() { PatternTimeout = TimeSpan.FromMilliseconds(1) };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AgentEntry Claude => AgentEntries.ClaudeCode with { SessionIdPattern = Slow };

    private static ArgvInputs Inputs() =>
        new(A.FirstItemId, "/work/repo", "sindri", ArgvBuilder.Seed(A.FirstItemId), SessionId.From(_id));

    [Fact]
    public void Test_tells_a_match_a_mismatch_a_bad_pattern_and_a_timeout_apart()
    {
        Expect.Equal(
            (PatternOutcome.Matched, PatternOutcome.NoMatch, PatternOutcome.NoMatch, PatternOutcome.TimedOut),
            (SessionIdRules.Test("[0-9]+", "123"), SessionIdRules.Test("[0-9]+", "12x"), SessionIdRules.Test("(", "1"), SessionIdRules.Test(Slow, _id, _tight)));
    }

    [Fact]
    public void A_timed_out_pattern_is_not_usable_and_not_a_mismatch()
    {
        Expect.Equal(PatternOutcome.TimedOut, SessionIdRules.Check(Claude, SessionId.From(_id), _tight));
        Expect.Equal(PatternOutcome.NoMatch, SessionIdRules.Check(Claude, id: null));
    }

    [Theory]
    [InlineData(LaunchMode.Open)]
    [InlineData(LaunchMode.Resume)]
    public void A_timed_out_pattern_is_refused_with_its_own_reason_not_as_a_mismatch(LaunchMode mode)
    {
        var argv = ArgvBuilder.Build(Claude, mode, Inputs(), _tight);

        Expect.Equal(SindriErrors.LaunchSessionIdTimedOut.Code, argv.Error.Code);
    }

    // A mismatch is judged under a roomy limit: with the 1 ms one, a cold or loaded run can time out "[0-9]+" and answer SND-LAUNCH-030.
    [Fact]
    public void A_mismatch_is_still_a_plain_invalid_request()
    {
        var argv = ArgvBuilder.Build(AgentEntries.ClaudeCode with { SessionIdPattern = "[0-9]+" }, LaunchMode.Open, Inputs(), TimeSpan.FromSeconds(5));

        Expect.Equal(SindriErrors.RequestInvalid.Code, argv.Error.Code);
    }

    [Fact]
    public void A_file_scan_whose_pattern_times_out_says_so_and_finds_no_id()
    {
        var rule = new CaptureRule(Slow, "/x/*", "rollout-(a+)\\.jsonl$");

        var scan = SessionIdCapture.Choose(rule, [$"/x/rollout-{_id}.jsonl"], new HashSet<string>(StringComparer.Ordinal), _tight);

        Expect.Equal((null, false, true), (scan.Id?.Value, scan.Ambiguous, scan.TimedOut));
    }

    [Fact]
    public async Task A_heartbeat_whose_id_pattern_times_out_is_refused_loudly_but_still_counts_as_alive()
    {
        await _s.Started();
        var events = _s.Events(Claude);

        var beat = await events.Beat(A.FirstLaunchId, SessionId.From(_id), Token);

        Expect.Equal(SindriErrors.LaunchSessionIdTimedOut.Code, beat.Error.Code);
        Expect.Equal(1, _s.Liveness.Silent(_s.Now.AddSeconds(130), LaunchRules.LostAfter).Count);   // beaten at Now, so silent only after 120 s
        Expect.Equal(0, _s.Liveness.Silent(_s.Now.AddSeconds(100), LaunchRules.LostAfter).Count);
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);   // no id recorded
    }

    [Fact]
    public async Task An_exit_whose_id_pattern_times_out_still_ends_the_session_and_says_why()
    {
        await _s.Started();
        var exit = new SessionExitService(_s.Events(Claude), new RecordingUsageSampler(_s));

        var result = await exit.Handle(A.SessionExitRequest().WithSessionId(_id).Build(), Token);

        Expect.Equal(SindriErrors.LaunchSessionIdTimedOut.Code, result.Error.Code);
        _ = _s.Views.Slice.Events.Payload<SessionEnded>(Assert.Single(_s.Views.Slice.Store.Events));
    }
}
