using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Runs git for the engine's adapters (the claim ref, the run diff): an argument list, never a shell string, each call bounded to 30 s and never left running.</summary>
internal static class GitProcess
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(30);

    /// <summary>The full argument list of one git call.</summary>
    /// <param name="directory">The repository or worktree git runs in.</param>
    /// <param name="args">The git command and its arguments.</param>
    /// <param name="inWorktree">True for a call in an agent's worktree: its content is the agent's, so hooks, fsmonitor and the ownership check are settled by the engine on the command line (engine P8 D10, sandbox spec E1-S2).</param>
    /// <returns>The arguments after 'git'.</returns>
    internal static string[] Arguments(string directory, string[] args, bool inWorktree)
    {
        return inWorktree
            ? ["-C", directory, "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=false", "-c", "safe.directory=" + directory, .. args]
            : ["-C", directory, .. args];
    }

    internal static async Task<GitRun> Run(string repository, string[] args, CancellationToken cancellationToken, bool inWorktree = false)
    {
        var start = new ProcessStartInfo("git", Arguments(repository, args, inWorktree))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process? git;
        try
        {
            git = Process.Start(start);
        }
        catch (Win32Exception e) // boundary: no git on PATH is an expected failure the activity retries
        {
            return new GitRun(-1, string.Empty, $"git can't run: {e.Message}");
        }

        using (git)
        {
            return git is null ? new GitRun(-1, string.Empty, "git didn't start") : await Answer(git, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<GitRun> Answer(Process git, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_budget);
        try
        {
            var output = git.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = git.StandardError.ReadToEndAsync(deadline.Token);
            await git.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return new GitRun(git.ExitCode, (await output.ConfigureAwait(false)).Trim(), await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            git.Kill();   // the budget or the caller's cancellation: never leave git running
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new GitRun(-1, string.Empty, "git didn't answer within 30 s");
        }
    }
}
