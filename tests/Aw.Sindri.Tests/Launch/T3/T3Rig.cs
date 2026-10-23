using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>A T3 host over the fake T3, with a scratch home for its session map, a repo that is a T3 project and a worktree inside it.</summary>
internal sealed class T3Rig : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-t3-").FullName;

    public T3Rig()
    {
        _ = Directory.CreateDirectory(Worktree);
        Api.AddProject(Repo);
    }

    public FakeT3Api Api { get; } = new();

    public FakeT3RepoIdentities Repos { get; } = new();

    public string Repo => Path.Combine(_home, "repo");

    public string Worktree => Path.Combine(Repo, ".claude", "worktrees", "sindri-one");

    /// <summary>'true' opens nothing and exits 0, so the viewer's open succeeds without a T3 app.</summary>
    public T3Options Options => new(Path.Combine(_home, "launch", "t3"), Path.Combine(_home, "t3home"), "t3") { Opener = "true" };

    public T3SessionHost Host() => new(Api, new T3SessionMap(Options), Options, Repos);

    public HostSession Session(string name, string? workDir = null) =>
        new(name, workDir ?? Worktree, ["sindri", "session", "wrap"], new Dictionary<string, string>(StringComparer.Ordinal))
        {
            RepoPath = Repo,
            Seed = "Sindri item ITM-1.",
            Title = "sindri ITM-1",
        };

    public void Dispose() => Directory.Delete(_home, recursive: true);
}
