using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.StartSession;

/// <summary>The ports a one-off start needs, as doubles: a profile with the repo 'demo', a catalog with Claude Code and Codex, a fake tmux host, a fake worktree maker and an environment map.</summary>
internal sealed class StartRig : IDisposable
{
    public const string RepoPath = "/work/demo";

    public StartRig()
    {
        var profile = SliceHarness.Profile();
        _ = Base.Profiles.Save(profile with { Profile = profile.Profile with { Repos = [new RepoEntry("demo", RepoPath)] } }, Token).AsTask().GetAwaiter().GetResult();
        _ = Base.HoldLease().GetAwaiter().GetResult();
        Checks.Folders.Add(RepoPath);
        _human = Base.Callers.Enter(A.Caller().Human().Build());
    }

    private readonly IDisposable _human;

    public void Dispose() => _human.Dispose();

    /// <summary>Runs the next calls as an agent session: starting an agent is for the person (agents spec §15.2).</summary>
    public IDisposable AsSession() => Base.Callers.Enter(A.Caller().Session().Build());

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public SliceHarness Base { get; } = new();

    public FakeSessionHost Host { get; } = new();

    public FakeStartChecks Checks { get; } = new();

    public FakeOneOffWorktrees Worktrees { get; } = new();

    public FakeStartChoices Choices { get; } = new();

    public FakeEnvironment Environment { get; } = new();

    public IAgentCatalog Catalog { get; } = new AgentCatalog(SessionData.Agents);

    public StartedSessions Book => field ??= new(Base.Store, Base.Events);

    public StartSessionService Service() =>
        new(
            new StartPreflight(Base.Profiles, Catalog, Checks, Host),
            new StartLaunch(Worktrees, Host, Host, Environment, Base.Clock),
            new StartRecord(Choices, Base.Store, Base.Held, Base.Events, new OpenGate()),
            Host, Book, Base.Callers);

    public Features.RemoveSessionWorktree.RemoveSessionWorktreeService RemoveService() => new(Book, Worktrees, Base.Callers, Host);
}
