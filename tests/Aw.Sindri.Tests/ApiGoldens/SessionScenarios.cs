using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sandbox.Plan;
using Aw.Sindri.Tests.Sessions;
using Aw.Sindri.Tests.SessionWrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The session routes' goldens (collector spec CA-7): the snapshot is published straight into the host's in-memory store, as the collector's pass would, so the bodies depend on nothing
/// the box runs. The Claude row is in tmux under Warp, the Codex row asks for approval, the Cursor row has an unreported id and a launch.
/// </summary>
internal static class SessionScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("sessions", "empty"), ("sessions", "fixture-three-providers"), ("sessions", "session-scoped"), ("sessions", "problems-source-failed"),
        ("sessions", "recently-ended"), ("sessions", "unsupported-platform"), ("sessions", "proc-unreadable"), ("sessions", "disabled"),
        ("sessions-id", "claude-tmux"), ("sessions-id", "codex-hook"), ("sessions-id", "not-found"), ("sessions-id", "session-caller-hidden-fields"), ("sessions-id", "cursor-engine-launch"),
        ("status", "sessions"),
        ("sessions", "sandboxed"), ("sessions-id", "cursor-sandboxed"), ("sessions-id", "cursor-sandbox-unavailable"),
    ];

    /// <summary>The golden host's profile: the repos the fixture sessions work in, so a session's worktree is served relative to its checkout.</summary>
    public const string ProfileYaml = "schemaVersion: 1\nuser: golden\nrepos:\n  - { name: api, path: /work/api }\n  - { name: web, path: /work/web }\nservices:\n  heartbeat: { every: 5s }\n";

    private static readonly string[] _engineLaunch = ["fixture-three-providers", "cursor-engine-launch", "sandboxed", "cursor-sandboxed", "cursor-sandbox-unavailable"];
    private static readonly string[] _sandboxed = ["sandboxed", "cursor-sandboxed", "cursor-sandbox-unavailable"];

    /// <summary>What the fake cgroup answers for the sandboxed launch's scope: 2.1 GiB in use.</summary>
    internal const long MemoryInUse = 2_254_857_830;

    /// <summary>What 'launch.started' records for the sandboxed scenarios' Step: writes to the fixture worktree, 12 GiB of memory.</summary>
    internal static SandboxSummary SandboxSummary { get; } = new(
        SandboxMechanism.Bubblewrap, "9f2c0a41d8e6", [Req.Worktree], ["model", "packages"], NetworkMode.Enforce, InnerState.On, new SandboxLimitsSummary("12GiB", "400%", 2048, "70m"),
        new SandboxCredentialsSummary(GithubCredentials.None), ModelLoginSource.OwnerCopy);

    /// <summary>The plan file the launcher stored for it: the fixture Step's plan with 12 GiB of memory.</summary>
    internal static SandboxPlan SandboxPlan { get; } = SandboxPlannerTests.Build(Req.WriteScope) with { Limits = SandboxPlannerTests.Build(Req.WriteScope).Limits with { MemoryMaxBytes = 12L * 1024 * 1024 * 1024 } };

    private static readonly Dictionary<string, UnavailableReason> _reasons = new(StringComparer.Ordinal)
    {
        ["unsupported-platform"] = UnavailableReason.UnsupportedPlatform,
        ["proc-unreadable"] = UnavailableReason.ProcUnreadable,
        ["disabled"] = UnavailableReason.Disabled,
    };

    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Whether the scenario needs the host's cgroup reader faked (it reads the sandboxed launch's scope).</summary>
    public static bool NeedsCgroup(string slug, string scenario) => Handles(slug, scenario) && _sandboxed.Contains(scenario, StringComparer.Ordinal);

    /// <summary>The host of a sandboxed scenario: the cgroup reader answers a live scope using <see cref="MemoryInUse"/>.</summary>
    public static Task<GoldenHost> Start(CancellationToken ct)
    {
        var stats = new FakeCgroupStats { Sample = new CgroupSample(OomKills: 0, MemoryCurrent: MemoryInUse, MemoryPeak: MemoryInUse, Pids: 12, CpuMicroseconds: 0) };
        return GoldenHost.Start(temporary: true, ct, ProfileYaml, extra: services => services.Replace(ServiceDescriptor.Singleton<ICgroupStats>(stats)));
    }

    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (sessions, ended, problems) = Snapshot(scenario);
        host.App.Services.GetRequiredService<ISessionSnapshot>().Publish(new SessionSnapshot(sessions, ended, problems, SessionData.T0.AddMinutes(5)) { History = History() });
        var human = Human(host.Token.Value);
        var hash = (await host.LiveHash(ct)).Value;   // the service reads repo paths from the approved profile, so approve it first
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), human));
        Assert.True(approved.Status == System.Net.HttpStatusCode.OK, approved.Body);
        if (_engineLaunch.Contains(scenario, StringComparer.Ordinal))
        {
            // The Cursor row's launch is the engine's: its instance, Step and attempt come from the 'launches' view, folded from the ledger.
            var item = "ITM-00000000000000000000000000000001";
            var sandboxed = _sandboxed.Contains(scenario, StringComparer.Ordinal);
            await LaunchScenarios.Started(host, LaunchId.From(SessionsRig.Launch), item, ViewerOutcomeKind.Skipped, ct, LaunchScenarios.ViewerOf("none", "none", ViewerOutcomeKind.Skipped),
                new LaunchStep(InstanceAddress.From(item + "/step/1"), "build", 2), agent: "cursor", sandbox: sandboxed ? SandboxSummary : null);
            if (sandboxed && scenario != "cursor-sandbox-unavailable")
            {
                var planFile = Infrastructure.Sandbox.LaunchPaths.PlanFile(Path.Combine(host.Home, "launch"), LaunchId.From(SessionsRig.Launch));   // the plan file the launcher stored; the unavailable scenario leaves it out
                _ = Directory.CreateDirectory(Path.GetDirectoryName(planFile)!);
                await File.WriteAllBytesAsync(planFile, AwJson.Serialize(SandboxPlan, host.App.Services.GetRequiredService<System.Text.Json.JsonSerializerOptions>()), ct);
            }

            await ViewScenarios.CatchUp(host, ct);
        }

        (string, string)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "api")];
        var (path, headers) = (slug, scenario) switch
        {
            ("sessions-id", "claude-tmux") => ($"/sindri/sessions/claude-code/{SessionsRig.ClaudeId}", human),
            ("sessions-id", "codex-hook") => ($"/sindri/sessions/codex/{SessionsRig.CodexId}", human),
            ("sessions-id", "cursor-engine-launch" or "cursor-sandboxed" or "cursor-sandbox-unavailable") => ("/sindri/sessions/cursor/pid-333-100", human),
            ("sessions-id", "not-found") => ("/sindri/sessions/codex/not-here", human),
            ("sessions-id", _) => ($"/sindri/sessions/claude-code/{SessionsRig.ClaudeId}", session),
            ("status", _) => ("/sindri/status", human),
            (_, "session-scoped") => ("/sindri/sessions", session),
            _ => ("/sindri/sessions", human),
        };
        return (await host.Http.Ask(Get(path, [.. headers]))).Body;
    }

    // Claude went idle, then asked for approval, then was approved: pins the entry shape and its oldest-first order for U11a's sheet.
    private static Dictionary<SessionRef, IReadOnlyList<SessionStateEntry>> History() => new()
    {
        [SessionsRig.Claude().Ref] =
        [
            new SessionStateEntry(SessionState.Idle, Reason: null, SessionData.T0.AddMinutes(-2), StateSource.Hook),
            new SessionStateEntry(SessionState.Asking, AskingReason.Approval, SessionData.T0.AddMinutes(-1), StateSource.Hook),
            new SessionStateEntry(SessionState.Running, Reason: null, SessionData.T0, StateSource.Record),
        ],
    };

    private static (IReadOnlyList<ObservedSession>, IReadOnlyList<EndedSession>, IReadOnlyList<SessionProblem>) Snapshot(string scenario) => scenario switch
    {
        "empty" => ([], [], []),
        "problems-source-failed" or "sessions" => (SessionsRig.Three, [], [new SessionProblem("claude-records", "SND-SESSION-001", "The claude-records source couldn't be read in the last pass")]),
        "recently-ended" => ([SessionsRig.Codex()], [new EndedSession(SessionsRig.Claude(), SessionData.T0.AddMinutes(4))], []),
        "unsupported-platform" or "proc-unreadable" or "disabled" =>
            (SessionsRig.Three, [], [new SessionProblem("processes", "SND-SESSION-002", $"Sindri can't see agent sessions on this system: {scenario}") { Reason = _reasons[scenario] }]),
        _ => (SessionsRig.Three, [], []),
    };
}
