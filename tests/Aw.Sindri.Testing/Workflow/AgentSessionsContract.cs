using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IAgentSessions"/> honours (engine P8 D3, D9): a launch is live until its end is recorded, an end says how,
/// and spend is the latest cumulative sample.
/// </summary>
public abstract class AgentSessionsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The sessions under test.</summary>
    /// <returns>The sessions.</returns>
    protected abstract ValueTask<IAgentSessions> Create();

    /// <summary>Records the end of <paramref name="launch"/>'s session.</summary>
    /// <param name="sessions">The sessions from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="reason">Exit or lost.</param>
    /// <param name="exitCode">The exit code for an exit.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask End(IAgentSessions sessions, LaunchId launch, SessionEndReason reason, int? exitCode);

    /// <summary>Records the end of <paramref name="launch"/>'s session by a sandbox limit.</summary>
    /// <param name="sessions">The sessions from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="kind">Memory or wall.</param>
    /// <param name="limit">The limit.</param>
    /// <param name="peak">The most the session used.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask EndByLimit(IAgentSessions sessions, LaunchId launch, LimitKind kind, long limit, long peak);

    /// <summary>Records usage sample <paramref name="sample"/> of <paramref name="launch"/>.</summary>
    /// <param name="sessions">The sessions from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="sample">The sample's number, from 1.</param>
    /// <param name="tokens">Input and output tokens so far.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask Spend(IAgentSessions sessions, LaunchId launch, int sample, long tokens);

    /// <summary>A launch with no end is live, and has spent nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_launch_with_no_end_is_live_and_has_spent_nothing()
    {
        var sessions = await Create();

        var state = await sessions.Watch(A.FirstLaunchId, Token);
        var spent = await sessions.Usage(A.FirstLaunchId, Token);

        Expect.True(state.IsSuccess && state.Value.IsLive);
        Expect.True(spent.IsSuccess && spent.Value == 0);
    }

    /// <summary>An exit says its code; a lost session says lost.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_end_says_how_the_session_ended()
    {
        var sessions = await Create();
        var other = LaunchId.From("LCH-00000000000000000000000000000002");
        await End(sessions, A.FirstLaunchId, SessionEndReason.Exit, 3);
        await End(sessions, other, SessionEndReason.Lost, exitCode: null);

        var exit = await sessions.Watch(A.FirstLaunchId, Token);
        var lost = await sessions.Watch(other, Token);

        Expect.True(exit.IsSuccess && exit.Value is { IsLive: false, EndedAs: AgentSessionEnd.Exit, ExitCode: 3 });
        Expect.True(lost.IsSuccess && lost.Value is { IsLive: false, EndedAs: AgentSessionEnd.Lost, ExitCode: null });
    }

    /// <summary>Spend is the latest sample, and another launch's samples don't count.</summary>
    /// <returns>The test.</returns>
    /// <summary>A limit kill names which limit it was and the peak.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_limit_end_names_the_limit_and_the_peak()
    {
        var sessions = await Create();
        await EndByLimit(sessions, A.FirstLaunchId, LimitKind.Memory, 2_147_483_648, 2_300_000_000);

        var state = await sessions.Watch(A.FirstLaunchId, Token);

        Expect.True(state.IsSuccess && state.Value is { IsLive: false, EndedAs: AgentSessionEnd.Limit, ExitCode: null, Limit: { Kind: LimitKind.Memory, Limit: 2_147_483_648, Peak: 2_300_000_000 } });
    }

    [Fact]
    public async Task Spend_is_the_latest_cumulative_sample_of_this_launch()
    {
        var sessions = await Create();
        var other = LaunchId.From("LCH-00000000000000000000000000000002");
        await Spend(sessions, A.FirstLaunchId, 1, 150);
        await Spend(sessions, A.FirstLaunchId, 2, 380);
        await Spend(sessions, other, 1, 9_000);

        var spent = await sessions.Usage(A.FirstLaunchId, Token);

        Expect.True(spent.IsSuccess && spent.Value == 380);
    }
}
