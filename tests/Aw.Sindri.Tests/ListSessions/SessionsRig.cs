using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Sessions;
using Aw.Sindri.Tests.Views;
using TerminalFacts = Aw.Sindri.Core.Sessions.TerminalFacts;

namespace Aw.Sindri.Tests.ListSessions;

/// <summary>The snapshot, the catalog and the caller a session read test needs, with three sessions in three repos and one of them launched.</summary>
internal sealed class SessionsRig
{
    public const string ClaudeId = "aaaaaaaa-0000-4000-8000-000000000001";
    public const string CodexId = "bbbbbbbb-0000-4000-8000-000000000002";
    public const string Launch = "LCH-00000000000000000000000000000001";

    private static readonly DateTimeOffset _t0 = SessionData.T0;

    public SessionsRig()
    {
        var profile = SliceHarness.Profile();
        LaunchViews = new LaunchViews(Launches.Views);
        _ = Base.Profiles.Save(profile with { Profile = profile.Profile with { Repos = [new RepoEntry("api", "/work/api"), new RepoEntry("web", "/work/web")] } }, Token).AsTask().GetAwaiter().GetResult();
    }

    public SliceHarness Base { get; } = new();

    /// <summary>The 'launches' view the session reads look launches up in; fold a launch into it with <see cref="LaunchFolds"/>.</summary>
    public ViewsHarness Launches { get; } = new();

    /// <summary>The read port over <see cref="Launches"/>.</summary>
    public ILaunchViews LaunchViews { get; set; } = null!;

    /// <summary>What the plan-file and cgroup reader answers for a sandboxed launch.</summary>
    public FakeSessionSandboxes Sandboxes { get; } = new();

    /// <summary>A launches view whose every read fails, as a locked or half-rebuilt views.db does.</summary>
    public sealed class UnreadableLaunches : ILaunchViews
    {
        private static readonly Kernel.Error _broken = Kernel.KernelErrors.Unavailable.With("views.db is locked");

        public ValueTask<Kernel.Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<LaunchRow?>>(_broken);

        public ValueTask<Kernel.Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<IReadOnlyList<LaunchRow>>>(_broken);

        public ValueTask<Kernel.Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<IReadOnlyList<LaunchRow>>>(_broken);

        public ValueTask<Kernel.Result<IReadOnlyList<LaunchRow>>> Since(DateTimeOffset since, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<IReadOnlyList<LaunchRow>>>(_broken);
    }

    public InMemorySessionSnapshot Snapshot { get; } = new();

    public IAgentCatalog Catalog { get; } = new AgentCatalog(SessionData.Agents);

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A Claude session in repo 'api', in tmux under Warp, with a title, a directory below the repo and a lane.</summary>
    public static ObservedSession Claude() => SessionData.Session("claude-code", ClaudeId, SessionState.Running, since: _t0, pid: 111) with
    {
        IdKnown = true,
        Repo = RepoName.From("api"),
        Cwd = "/work/api/.claude/worktrees/ideas",
        Title = "private title",
        StartedAt = _t0.AddMinutes(-30),
        Team = "sindri",
        Lane = "ideas",
        Host = new HostInfo("tmux", "tmux · Warp")
        {
            Terminal = new TerminalFacts("warp", 99) { WarpFocusUrl = "warp://session/0123456789abcdef0123456789abcdef" },
            Tmux = new TmuxFacts("/tmp/tmux-1000/default", "default", "main", "%3") { AttachedClients = 1 },
        },
    };

    /// <summary>A Codex session in repo 'web', asking for approval of Bash.</summary>
    public static ObservedSession Codex() => SessionData.Session("codex", CodexId, SessionState.Asking, since: _t0.AddMinutes(1), pid: 222) with
    {
        IdKnown = true,
        Repo = RepoName.From("web"),
        Reason = AskingReason.Approval,
        Tool = "Bash",
        Cwd = "/work/web",
        Host = new HostInfo("warp", "Warp") { Terminal = new TerminalFacts("warp", 98) },
    };

    /// <summary>A Cursor session with no repo and an id made of its pid, launched by Sindri.</summary>
    public static ObservedSession Cursor() => SessionData.Session("cursor", "pid-333-100", SessionState.Idle, since: _t0.AddMinutes(2), pid: 333) with
    {
        IdKnown = false,
        Host = HostInfo.Unknown,
        LaunchId = LaunchId.From(Launch),
    };

    public static IReadOnlyList<ObservedSession> Three => [Claude(), Codex(), Cursor()];

    public SessionsRig Publish(IReadOnlyList<ObservedSession> sessions, IReadOnlyList<EndedSession>? ended = null, IReadOnlyList<SessionProblem>? problems = null)
    {
        Snapshot.Publish(new SessionSnapshot(sessions, ended ?? [], problems ?? [], _t0.AddMinutes(5)));
        return this;
    }

    public IDisposable Human() => Base.Callers.Enter(A.Caller().Human().Build());

    public IDisposable SessionOf(string repo) => Base.Callers.Enter(A.Caller().Session().InRepo(repo).Build());

    public IDisposable SessionWithoutRepo() => Base.Callers.Enter(A.Caller().Session().Build());
}
