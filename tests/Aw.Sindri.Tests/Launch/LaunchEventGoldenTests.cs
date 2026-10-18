using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// The ledger payload of each launch event, pinned as a golden through the same source-generated context the ledger writes with: a rename or a change of casing
/// or optionality would corrupt rows that can never be changed. AW_GOLDEN_UPDATE=1 rewrites a file.
/// </summary>
public sealed class LaunchEventGoldenTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 18, 9, 30, 0, TimeSpan.Zero);
    private static readonly AgentId _codex = AgentId.From("codex");
    private static readonly SessionId _session = SessionId.From("0a1b2c3d-0000-4000-8000-000000000001");
    private static readonly string[] _attach = ["tmux", "-L", "sindri-launch", "attach", "-t", "=snd-1"];

    public static TheoryData<string> Cases =>
    [
        "item.open-requested",
        "launch.started-opened",
        "launch.started-viewer-failed",
        "launch.failed-host-stage",
        "launch.failed-not-approved",
        "launch.failed-plain",
        "launch.viewer-attached",
        "launch.session-reported",
        "session.ended-exit-with-tokens",
        "session.ended-lost",
        "session.usage",
        "launch.probed",
    ];

    private static string Of<T>(SliceHarness h, string type, T payload) => ApiGolden.Reindent(h.Events.Entry(type, payload, "key", A.FirstItemId.Value).Envelope.Payload);

    private static string Json(SliceHarness h, string name) => name switch
    {
        "item.open-requested" => Of(h, EventTypes.ItemOpenRequested, new ItemOpenRequested(A.FirstLaunchId, A.FirstItemId, LaunchMode.Resume, _codex, "click")),
        "launch.started-opened" => Of(h, EventTypes.LaunchStarted, new LaunchStarted(A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, _codex, _session, "sindri " + A.FirstItemId.Value,
            new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null), _attach, CatalogSource.Builtin, "sha256:" + new string('a', 64))),
        "launch.started-viewer-failed" => Of(h, EventTypes.LaunchStarted, new LaunchStarted(A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, _codex, SessionId: null, "sindri " + A.FirstItemId.Value,
            new LaunchViewer("exec-template", "kitty", ViewerOutcomeKind.Failed, "SND-LAUNCH-001", "kitty: no display"), _attach, CatalogSource.Override, "sha256:" + new string('b', 64))),
        "launch.failed-host-stage" => Of(h, EventTypes.LaunchFailed,
            new LaunchFailed(A.FirstLaunchId, A.FirstItemId, "SND-LAUNCH-009", "tmux did not start the session within 4 s", new LaunchFailureDetail(LaunchStage.Host))),
        "launch.failed-not-approved" => Of(h, EventTypes.LaunchFailed, new LaunchFailed(A.FirstLaunchId, A.FirstItemId, "SND-LAUNCH-012", "aider isn't approved for unattended Steps",
            new LaunchFailureDetail(Stage: null, Reason: "hash-changed", Current: "sha256:" + new string('c', 64), Approved: ["sha256:" + new string('d', 64)]))),
        "launch.failed-plain" => Of(h, EventTypes.LaunchFailed, new LaunchFailed(A.FirstLaunchId, A.FirstItemId, "SND-LAUNCH-007", "Sindri restarted before the launch started (fix: open the item again)")),
        "launch.viewer-attached" => Of(h, EventTypes.LaunchViewerAttached, new LaunchViewerAttached(A.FirstLaunchId, _at)),
        "launch.session-reported" => Of(h, EventTypes.LaunchSessionReported, new LaunchSessionReported(A.FirstLaunchId, _session)),
        "session.ended-exit-with-tokens" => Of(h, EventTypes.SessionEnded, new SessionEnded(A.FirstLaunchId, SessionEndReason.Exit, 0, InputTokens: 1200, OutputTokens: 340)),
        "session.ended-lost" => Of(h, EventTypes.SessionEnded, new SessionEnded(A.FirstLaunchId, SessionEndReason.Lost, ExitCode: null)),
        "session.usage" => Of(h, EventTypes.SessionUsage, new SessionUsage(A.FirstLaunchId, Sample: 3, InputTokens: 1200, OutputTokens: 340, _codex, Model: "gpt-5-codex")),
        "launch.probed" => Of(h, EventTypes.LaunchProbed, new LaunchProbed(
            new TerminalFacts("warp", "Warp", Present: true, "detected", ConfigPath: null, Problem: null, Detail: null),
            new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null),
            [new TargetFacts(_codex, Present: true, "0.40.0", Limited: "usage-unverified", Problem: null, CatalogSource.Builtin, Overridden: false)],
            [new RepoFacts(RepoName.From("demo"), Present: true)])),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public void The_ledger_payload_equals_its_golden(string name) =>
        Golden.Match(Json(new SliceHarness(), name), typeof(LaunchEventGoldenTests).Assembly, $"Golden/ledger/{name}.json", Normalization.None);

    [Fact, Trait("Lane", "unit")]
    public void Every_launch_event_has_a_golden()
    {
        string[] types = [EventTypes.LaunchProbed, EventTypes.ItemOpenRequested, EventTypes.LaunchStarted, EventTypes.LaunchFailed, EventTypes.SessionEnded, EventTypes.SessionUsage, EventTypes.LaunchViewerAttached, EventTypes.LaunchSessionReported];

        Assert.All(types, type => Assert.Contains(Cases, c => c.Data.StartsWith(type, StringComparison.Ordinal)));
    }
}
