using System.Globalization;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Tests.Agents;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Agents list's wire shape (Agents page spec §3.1, §8, phase A0): the rows as a client reads them, with <c lang="text">reasons[]</c>. No route serves this yet (the session-rows PR, A1, joins the fields to
/// <c lang="text">GET /sindri/sessions</c>), so the body is the rows serialized through the host's own JSON options, which pins the field names, order and enum words the route will keep.
/// 'needs-you' is the spec's §4.3 walk (tests/fixtures/agents/needs-you.json); 'every-reason' holds one row for each reason, each state and a builder.
/// </summary>
internal static class AgentScenarios
{
    /// <summary>The (slug, scenario) pairs of this class. The slug 'agents-rows' has no route behind it until A1 serves the list; A1 swaps it for the real route's slug.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [("agents-rows", "needs-you"), ("agents-rows", "every-reason")];

    private static readonly DateTimeOffset _t0 = new(2026, 11, 12, 9, 10, 0, TimeSpan.Zero);

    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    public static Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var list = scenario == "needs-you" ? AgentWalk.Rows(AgentWalk.At, asked: false) : EveryReason();
        return Task.FromResult(Encoding.UTF8.GetString(AwJson.Serialize(list, host.App.Services.GetRequiredService<System.Text.Json.JsonSerializerOptions>())));
    }

    /// <summary>The rows of the 'every-reason' scenario, in the page order of spec §4.1: Needs you, Working, Waiting, Idle.</summary>
    internal static AgentRowList EveryReason() => new(
        [
            Row("session:claude-code/s-ux", AgentState.WaitingOnYou, Lane("ux", 4) with { State = SessionState.Asking, Asking = new SessionAsking(AskingReason.Input, Tool: null, _t0.AddMinutes(-4)) }),
            Row("session:codex/s-api", AgentState.WaitingOnYou, Lane("api", 2) with { State = SessionState.Asking, IsClaudeLane = false, Asking = new SessionAsking(AskingReason.Auth, Tool: null, _t0.AddMinutes(-2)) }),
            Row("session:claude-code/s-qa", AgentState.WaitingOnYou, Lane("qa", 5) with { State = SessionState.Asking, Asking = new SessionAsking(AskingReason.Approval, "Bash", _t0.AddSeconds(-30)) }),
            Row("lane:joi/design", AgentState.Down, Lane("design", 30) with { State = SessionState.Idle, HasLiveSession = false }),
            Row("session:claude-code/s-ideas", AgentState.Working, Lane("ideas", 30) with
            {
                ContextTokens = 210_000,
                AtRecycleMark = true,
                RecycleSince = new DateTimeOffset(2026, 11, 12, 8, 50, 0, TimeSpan.Zero),
                QueuedPrs = [new AgentQueuedPr(3078, AgentGate.Pass, InGithubQueue: false, new DateTimeOffset(2026, 11, 12, 8, 52, 0, TimeSpan.Zero))],
            }),
            Row("session:claude-code/s-meta", AgentState.Idle, Lane("meta", 15) with { State = SessionState.Idle, IsMetaLane = true, OpenObligations = 2 }),
            Row("session:claude-code/s-ui", AgentState.Idle, Lane("ui", 14) with
            {
                State = SessionState.Idle,
                QueuedPrs = [new AgentQueuedPr(3067, AgentGate.Fail, InGithubQueue: false, _t0.AddMinutes(-30))],
            }),
            Row("session:claude-code/s-perf-b1", AgentState.Working, Lane("perf", 25), title: "perf builder", parent: "session:claude-code/s-perf"),
            Row("session:claude-code/s-tools", AgentState.Limited, Lane("tools", 20) with
            {
                UsageLimited = true,
                UsageResetsAt = new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.Zero),
                UsageSince = new DateTimeOffset(2026, 11, 12, 8, 50, 0, TimeSpan.Zero),
            }),
            Row("job:4242", AgentState.Working, Lane("tools", 39) with { HeavyWait = new AgentHeavyWait(_t0.AddMinutes(-39), Held: 3, Cap: 3) }, title: "heavy: dotnet test", parent: "session:claude-code/s-tools"),
            Row("session:claude-code/s-engine", AgentState.Idle, Lane("engine", 10) with { State = SessionState.Idle, Estimated = true, StateSource = AgentStateSource.Transcript }),
        ],
        []);

    private static AgentReasonInputs Lane(string name, int stateMinutes) =>
        new(_t0, name, _t0.AddMinutes(-stateMinutes)) { Lane = name, ContextTokens = 60_000 };

    private static AgentRow Row(string id, AgentState state, AgentReasonInputs inputs, string? title = null, string? parent = null)
    {
        var reasons = AgentReasons.Evaluate(inputs);
        var live = inputs.HasLiveSession && !id.StartsWith("job:", StringComparison.Ordinal);
        var facts = new AgentFacts(
            inputs.Lane, "agentic-workflow", inputs.AtRecycleMark ? "docs/agents-page-spec" : null, inputs.QueuedPrs.Count > 0 ? string.Create(CultureInfo.InvariantCulture, $"PR #{inputs.QueuedPrs[0].Number}") : null,
            inputs.ContextTokens, inputs.AtRecycleMark, inputs.HeavyWait is { } wait ? string.Create(CultureInfo.InvariantCulture, $"{wait.Held} of {wait.Cap}") : null);
        return new AgentRow(
            AgentRowId.From(id), Version: 1, state, inputs.StateSource, _t0, inputs.Estimated, title ?? inputs.Name, facts, reasons, parent, inputs.Asking?.Since ?? inputs.StateSince,
            live ? _t0.AddSeconds(-20) : null, Blocks: null, Actions(inputs, reasons), [.. inputs.QueuedPrs.Select(static p => string.Create(CultureInfo.InvariantCulture, $"pr:{p.Number}"))]);
    }

    private static string[] Actions(AgentReasonInputs inputs, IReadOnlyList<AgentReason> reasons) => reasons.Count == 0
        ? (inputs.HasLiveSession ? ["jump"] : ["copy-launch-command"])
        : reasons[0].Kind switch
        {
            AgentReasonKind.LaneNoSession => ["copy-launch-command"],
            AgentReasonKind.RecycleMark => ["copy-digest-request", "recycle", "jump"],
            AgentReasonKind.IdleOwed when inputs.QueuedPrs.Count > 0 => ["jump", "open-in-home"],
            AgentReasonKind.UsageLimit or AgentReasonKind.SlotWait => ["open-peek"],
            AgentReasonKind.PrReady => ["open-in-home", "jump"],
            AgentReasonKind.PermissionPrompt or AgentReasonKind.OwnerQuestion or AgentReasonKind.Login or AgentReasonKind.IdleOwed => ["jump"],
        };
}
