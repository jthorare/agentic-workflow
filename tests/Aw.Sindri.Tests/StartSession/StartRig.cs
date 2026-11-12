using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Features.StartSession;
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

    public IAgentCatalog Catalog { get; } = new Aw.Sindri.Infrastructure.Launch.AgentCatalog(SessionData.Agents);

    public StartedSessions Book => field ??= new(Base.Store, Base.Events);

    public StartSessionService Service() =>
        new(Base.Profiles, Catalog, Checks, Worktrees, Host, Host, Environment, Choices, Book, Base.Callers, Base.Clock, Base.Store, Base.Held, Base.Events, new OpenGate());

    public Aw.Sindri.Features.RemoveSessionWorktree.RemoveSessionWorktreeService RemoveService() => new(Book, Worktrees, Base.Callers);
}

internal sealed class FakeStartChecks : IStartChecks
{
    public HashSet<string> Folders { get; } = new(StringComparer.Ordinal);

    public bool Installed { get; set; } = true;

    public bool SignedIn { get; set; } = true;

    public ValueTask<AgentReadiness> Agent(AgentEntry agent, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AgentReadiness(Installed, SignedIn, agent.Bin + " login"));

    public bool FolderExists(string path) => Folders.Contains(path);
}

internal sealed class FakeOneOffWorktrees : IOneOffWorktrees
{
    private readonly HashSet<string> _live = new(StringComparer.Ordinal);

    public Error? MakeFails { get; set; }

    public string? Status { get; set; }

    public List<string> Names { get; } = [];

    public List<string> Removed { get; } = [];

    public IReadOnlySet<string> Live => _live;

    public ValueTask<Result<OneOffWorktree>> Make(string repoPath, string name, CancellationToken cancellationToken)
    {
        if (MakeFails is { } failure)
        {
            MakeFails = null;
            return ValueTask.FromResult<Result<OneOffWorktree>>(failure);
        }

        Names.Add(name);
        var path = "/wt/demo/" + name;
        _ = _live.Add(path);
        return ValueTask.FromResult<Result<OneOffWorktree>>(new OneOffWorktree(path, name));
    }

    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
    {
        if (Status is not null)
        {
            return ValueTask.FromResult<Result<Unit>>(Aw.Sindri.Core.SindriErrors.WorktreeDirty.With("uncommitted changes"));
        }

        Removed.Add(path);
        _ = _live.Remove(path);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    public ValueTask<string> StatusSummary(string path, CancellationToken cancellationToken) => ValueTask.FromResult(Status ?? string.Empty);
}

internal sealed class FakeStartChoices : IStartChoices
{
    public List<(string Repo, string Agent)> Remembered { get; } = [];

    public ValueTask Remember(string repo, string agent, CancellationToken cancellationToken)
    {
        Remembered.Add((repo, agent));
        return ValueTask.CompletedTask;
    }

    public ValueTask<StartChoice> Last(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Remembered.Count == 0 ? new StartChoice(null, null) : new StartChoice(Remembered[^1].Repo, Remembered[^1].Agent));
}

internal sealed class FakeEnvironment : IEnvironmentReader
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal)
    {
        ["HOME"] = "/home/thor", ["PATH"] = "/usr/bin", ["TERM"] = "xterm", ["AW_STATE_DIR"] = "/state", ["AW_FLEET_DIR"] = "/state/fleet",
        ["AW_SINDRI_TOKEN"] = "secret", ["GH_TOKEN"] = "gh-secret", ["ANTHROPIC_API_KEY"] = "sk-a", ["OPENAI_API_KEY"] = "sk-o",
    };

    public string? Get(string name) => Values.GetValueOrDefault(name);
}

internal sealed class OpenGate : Aw.Sindri.Core.Commands.ICommandGate
{
    public ValueTask<IDisposable?> Enter(CancellationToken cancellationToken) => ValueTask.FromResult<IDisposable?>(new Hold());

    private sealed class Hold : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
