using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Runs git for the engine's adapters (the claim ref, the run diff): an argument list, never a shell string, each call bounded to 30 s and never left running.</summary>
internal static class GitProcess
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs git in <paramref name="repository"/>. <paramref name="hardened"/> is for a folder the owner just picked and Sindri has not trusted: no fsmonitor and no hooks path ('-c core.fsmonitor= -c core.hooksPath=/dev/null') and no
    /// system config, so reading it runs nothing the folder brings.
    /// </summary>
    internal static async Task<GitRun> Run(string repository, string[] args, CancellationToken cancellationToken, bool hardened = false)
    {
        string[] guard = hardened ? ["-c", "core.fsmonitor=", "-c", "core.hooksPath=/dev/null"] : [];
        var start = new ProcessStartInfo("git", ["-C", repository, .. guard, .. args])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["LC_ALL"] = "C";   // git's messages are matched in English
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";   // never wait for a credential
        if (hardened)
        {
            start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        }

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
