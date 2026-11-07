using System.Diagnostics;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

internal sealed partial class GitCli
{
    /// <summary>Streams 'git archive --format=tar' of a commit to <paramref name="consume"/> (tracked files only, never the working tree).</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A commit sha.</param>
    /// <param name="consume">Reads the tar stream to its end.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit, or SND-CONTEXT-010 when git fails or the consumer does.</returns>
    public async ValueTask<Result<Unit>> Archive(string repo, string commit, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var start = CleanGit(["-C", repo, "archive", "--format=tar", commit]);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_archiveTimeout);
        Process? git = null;
        try
        {
            git = Process.Start(start) ?? throw new InvalidOperationException("git didn't start");
            var stderr = git.StandardError.ReadToEndAsync(timeout.Token);
            await ConsumeToEnd(git.StandardOutput.BaseStream, consume, timeout.Token).ConfigureAwait(false);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            // git is gone, so everything it wrote is already in the pipe. Its end of stderr closed with it; a stray fork of a parallel
            // process may still hold another copy, and waiting for that EOF held this call to the whole archive timeout (a gate hung 5 min here).
            _ = await Task.WhenAny(stderr, Task.Delay(_drainGrace, timeout.Token)).ConfigureAwait(false);
            return git.ExitCode == 0 ? Unit.Value : Failed("archive", git.ExitCode);
        }
        catch (OperationCanceledException)
        {
            Kill(git);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return ContextErrors.SourceUnavailable.With("git archive didn't finish in time");
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or InvalidDataException)
        {
            Kill(git);
            return ContextErrors.SourceUnavailable.With($"git archive didn't run: {e.GetType().Name}");
        }
        finally
        {
            git?.Dispose();
        }
    }

    // A TarReader stops at the end-of-archive marker and leaves git's record padding in the pipe. On a loaded box (the user over
    // pipe-user-pages-soft) a new pipe holds one page, so git blocked writing the rest and never exited (a gate hung here 30 s).
    private static async Task ConsumeToEnd(Stream tar, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        await consume(tar, cancellationToken).ConfigureAwait(false);
        await tar.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
    }
}
