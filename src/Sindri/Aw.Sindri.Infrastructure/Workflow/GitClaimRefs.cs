using System.ComponentModel;
using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The claim's marker ref through git (engine spec §12 'effect.claim.ref'; engine P6 unit (d)): 'git update-ref' creates
/// the ref, with an empty old value so it never moves one that exists, at the default branch's head: the remote's
/// default ('origin/HEAD') when the repo has one, else 'main', else 'master', else the checkout's HEAD. No branch, index
/// or working-tree file is touched. Each git call is bounded to 30 s.
/// </summary>
internal sealed class GitClaimRefs : IClaimRefs
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(30);

    private static readonly string[] _defaultBranches = ["refs/remotes/origin/HEAD", "refs/heads/main", "refs/heads/master", "HEAD"];

    public async ValueTask<Result<bool>> Mark(string repository, string reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(reference);
        if (await Commit(repository, reference, cancellationToken).ConfigureAwait(false) is { Exit: 0 })
        {
            return false;   // the marker exists (an earlier claim, or this one retried): reused as it is
        }

        string? head = null;
        foreach (var branch in _defaultBranches)
        {
            if (await Commit(repository, branch, cancellationToken).ConfigureAwait(false) is { Exit: 0, Output: var sha })
            {
                head = sha;
                break;
            }
        }

        if (head is null)
        {
            return WorkflowErrors.NoRuntime.With($"{repository} has no commit to point {reference} at (fix: commit once on its default branch, then resume the parked instance to send the claim again)");
        }

        var created = await Git(repository, ["update-ref", "-m", "sindri: claim", reference, head, ""], cancellationToken).ConfigureAwait(false);
        if (created.Exit == 0)
        {
            return true;
        }

        if (await Commit(repository, reference, cancellationToken).ConfigureAwait(false) is { Exit: 0 })
        {
            return false;   // created by a concurrent take between the check and the write
        }

        // A lock another git holds passes: the error is retryable (the effect retries it). Anything else can't be fixed by trying again.
        return created.Error.Contains(".lock", StringComparison.Ordinal)
            ? WorkflowErrors.EffectFailed.With($"git update-ref {reference} in {repository} is locked: {created.Error.Trim()} (retrying)")
            : WorkflowErrors.NoRuntime.With($"git update-ref {reference} failed in {repository}: {created.Error.Trim()} (fix: check the repo, then resume the parked instance to send the claim again)");
    }

    /// <summary>The commit <paramref name="name"/> names, if it names one.</summary>
    private static Task<GitRun> Commit(string repository, string name, CancellationToken cancellationToken) =>
        Git(repository, ["rev-parse", "--verify", "--quiet", name + "^{commit}"], cancellationToken);

    private static async Task<GitRun> Git(string repository, string[] args, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git", ["-C", repository, .. args])
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

    private sealed record GitRun(int Exit, string Output, string Error);
}
