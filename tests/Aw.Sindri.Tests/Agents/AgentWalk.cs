using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Agents;

/// <summary>
/// The Agents page spec §4.3 walk, read from tests/fixtures/agents/needs-you.json: eleven lanes at 09:10, then qa asks for approval at 09:12:03. The fixture holds each lane's inputs and the
/// reasons the spec says hold; this class turns the lanes into <see cref="AgentReasonInputs"/> and <see cref="AgentRow"/> values.
/// </summary>
internal static class AgentWalk
{
    private static readonly Lazy<JsonObject> _doc = new(static () => (JsonObject)JsonNode.Parse(File.ReadAllText(Path()))!);   // why: the fixture is a repo file this test owns

    /// <summary>The fixture file.</summary>
    public static string Path()
    {
        var project = System.IO.Path.GetDirectoryName(Golden.SourcePath(typeof(AgentWalk).Assembly, "Agents"))!;   // why: a path under the project always has a parent
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(project, "..", "fixtures", "agents", "needs-you.json"));
    }

    /// <summary>The fixture's root object.</summary>
    public static JsonObject Doc => _doc.Value;

    /// <summary>The time of the first read, 09:10.</summary>
    public static DateTimeOffset At => Time(Doc["at"]!);

    /// <summary>The time of the first event, 09:12:03.</summary>
    public static DateTimeOffset EventAt => Time(Doc["events"]![0]!["at"]!);

    /// <summary>Reads an ISO time in the fixture.</summary>
    public static DateTimeOffset Time(JsonNode node) => DateTimeOffset.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    /// <summary>The lane objects, in the fixture's order.</summary>
    public static IReadOnlyList<JsonObject> Lanes => [.. Doc["lanes"]!.AsArray().Cast<JsonObject>()];

    /// <summary>The reasons the fixture lists for a lane in an <c lang="text">expect</c> block, as lines of "class kind since text"; empty when it lists none.</summary>
    public static IReadOnlyList<string> Expected(string block, string lane) =>
        Doc["expect"]![block]![lane] is JsonArray reasons ? [.. reasons.Select(static r => Line(r!))] : [];

    private static string Line(JsonNode reason) =>
        $"{reason["class"]} {reason["kind"]} {reason["since"]} {reason["text"]}";

    /// <summary>One reason as a line of "class kind since text", the form <see cref="Expected"/> uses.</summary>
    public static string Line(AgentReason reason) =>
        string.Create(CultureInfo.InvariantCulture, $"{reason.Class} {Kind(reason.Kind)} {reason.Since.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ} {reason.Text}");

    private static string Kind(AgentReasonKind kind) => kind switch
    {
        AgentReasonKind.PermissionPrompt => "permission-prompt",
        AgentReasonKind.OwnerQuestion => "owner-question",
        AgentReasonKind.Login => "login",
        AgentReasonKind.UsageLimit => "usage-limit",
        AgentReasonKind.LaneNoSession => "lane-no-session",
        AgentReasonKind.RecycleMark => "recycle-mark",
        AgentReasonKind.IdleOwed => "idle-owed",
        AgentReasonKind.SlotWait => "slot-wait",
        AgentReasonKind.PrReady => "pr-ready",
    };

    /// <summary>The inputs for a lane at <paramref name="now"/>; when <paramref name="asked"/> is true the fixture's event (qa asks for approval) has happened.</summary>
    public static AgentReasonInputs Inputs(JsonObject lane, DateTimeOffset now, bool asked)
    {
        var inputs = WithQueueAndThresholds(Basics(lane, now), lane);
        return asked && Doc["events"]![0]!["lane"]!.GetValue<string>() == inputs.Name ? WithEvent(inputs) : inputs;
    }

    private static AgentReasonInputs Basics(JsonObject lane, DateTimeOffset now)
    {
        var name = lane["lane"]!.GetValue<string>();
        return new AgentReasonInputs(now, name, Time(lane["stateSince"]!))
        {
            Lane = name,
            IsMetaLane = lane["isMeta"]?.GetValue<bool>() ?? false,
            State = lane["state"]!.GetValue<string>() == "idle" ? SessionState.Idle : SessionState.Running,
            ContextTokens = lane["contextTokens"]?.GetValue<int>(),
        };
    }

    private static AgentReasonInputs WithQueueAndThresholds(AgentReasonInputs inputs, JsonObject lane)
    {
        var settings = Doc["settings"]!;
        return inputs with
        {
            AtRecycleMark = lane["atRecycleMark"]?.GetValue<bool>() ?? false,
            RecycleSince = lane["recycleSince"] is { } since ? Time(since) : null,
            QueuedPrs = lane["queuedPrs"] is JsonArray prs ? [.. prs.Select(static p => Queued(p!))] : [],
            IdleOwedMinutes = settings["idleOwedMinutes"]!.GetValue<int>(),
            SlotWaitMinutes = settings["slotWaitMinutes"]!.GetValue<int>(),
        };
    }

    private static AgentQueuedPr Queued(JsonNode pr) =>
        new(pr["number"]!.GetValue<int>(), pr["gate"]!.GetValue<string>() == "pass" ? AgentGate.Pass : AgentGate.None, InGithubQueue: false, Time(pr["readyAt"]!));

    private static AgentReasonInputs WithEvent(AgentReasonInputs inputs)
    {
        var ev = Doc["events"]![0]!;
        return inputs with { State = SessionState.Asking, Asking = new SessionAsking(AskingReason.Approval, ev["tool"]!.GetValue<string>(), Time(ev["at"]!)) };
    }

    /// <summary>The 11 rows at <paramref name="now"/>, in the page order of spec §4.1 (Needs you, then Working, then Idle).</summary>
    public static AgentRowList Rows(DateTimeOffset now, bool asked)
    {
        var rows = Lanes.Select(l => Row(l, now, asked)).ToList();
        var needs = rows.Where(static r => r.Reasons.Count > 0 && r.Reasons[0].Class is not (2 or 6))
            .OrderBy(static r => r.Reasons[0].Class).ThenBy(static r => r.Reasons[0].Since).ThenBy(static r => r.Id.Value, StringComparer.Ordinal).ToList();
        var working = rows.Where(r => !needs.Contains(r) && r.State == AgentState.Working).OrderBy(static r => r.Facts.Lane, StringComparer.Ordinal);
        var idle = rows.Where(r => !needs.Contains(r) && r.State == AgentState.Idle).OrderBy(static r => r.Facts.Lane, StringComparer.Ordinal);
        return new AgentRowList([.. needs, .. working, .. idle], []);
    }

    private static AgentRow Row(JsonObject lane, DateTimeOffset now, bool asked)
    {
        var inputs = Inputs(lane, now, asked);
        var reasons = AgentReasons.Evaluate(inputs);
        var state = inputs.State switch
        {
            SessionState.Asking => AgentState.WaitingOnYou,
            SessionState.Idle => AgentState.Idle,
            SessionState.Unknown or SessionState.Running => AgentState.Working,
        };
        var facts = new AgentFacts(inputs.Lane, Doc["repo"]!.GetValue<string>(), lane["branch"]?.GetValue<string>(), lane["workRef"]?.GetValue<string>(), inputs.ContextTokens, inputs.AtRecycleMark, HeavySlot: null);
        return new AgentRow(
            AgentRowId.From("session:claude-code/" + lane["session"]!.GetValue<string>()), Version: 1, state, AgentStateSource.Hook, now, Estimated: false, inputs.Name, facts, reasons, Parent: null,
            inputs.Asking?.Since ?? inputs.StateSince, Time(lane["lastActivity"]!), Blocks: null, [.. lane["actions"]!.AsArray().Select(static a => a!.GetValue<string>())], [.. lane["links"]!.AsArray().Select(static a => a!.GetValue<string>())]);
    }
}
