using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Forge;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Reviews;

/// <summary>
/// <see cref="IFixSessions"/> from the collector's live sessions (PR review page spec §8.2 step 3): a session matches when its checkout's <c language="text">origin</c> is the head repository
/// <b>and</b> its branch is the head branch (the branch name alone would send a fork's 'main' to an unrelated session). Sessions Sindri launched (a launch id) are skipped; git runs
/// for at most 5 s per call and at most 20 sessions are looked at.
/// </summary>
/// <param name="snapshot">The collector's latest pass.</param>
/// <param name="runner">Runs git.</param>
internal sealed class GitFixSessions(ISessionSnapshot snapshot, IToolRunner runner) : IFixSessions
{
    private const int MaxSessions = 20;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public ValueTask<string?> OnBranch(string headRepo, string branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(headRepo);
        ArgumentNullException.ThrowIfNull(branch);
        var candidates = (snapshot.Current?.Sessions ?? []).Where(static s => s.LaunchId is null).Take(MaxSessions);
        foreach (var session in candidates)
        {
            if (session.Cwd is { Length: > 0 } cwd && Matches(cwd, headRepo, branch, cancellationToken))
            {
                return ValueTask.FromResult<string?>(session.Ref.Provider.Value + ", " + Tilde(cwd));
            }
        }

        return ValueTask.FromResult<string?>(null);
    }

    private bool Matches(string cwd, string headRepo, string branch, CancellationToken cancellationToken)
    {
        var origin = runner.Run("git", ["-C", cwd, "remote", "get-url", "origin"], _timeout, options: null, cancellationToken);
        if (origin.Outcome != ToolOutcome.Exited || origin.ExitCode != 0 || GitForgeRepos.Parse(origin.Output.Trim()) is not { } repo
            || !string.Equals($"{repo.Owner}/{repo.Name}", headRepo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var head = runner.Run("git", ["-C", cwd, "rev-parse", "--abbrev-ref", "HEAD"], _timeout, options: null, cancellationToken);
        return head.Outcome == ToolOutcome.Exited && head.ExitCode == 0 && string.Equals(head.Output.Trim(), branch, StringComparison.Ordinal);
    }

    private static string Tilde(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && (path == home || path.StartsWith(home + "/", StringComparison.Ordinal)) ? "~" + path[home.Length..] : path;
    }
}
