using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

internal sealed partial class GitCli
{
    /// <summary>git's exit when it doesn't know an option (git before 2.42 and --attr-source).</summary>
    private const int UsageExit = 129;

    /// <summary>
    /// A blob as a checkout of <paramref name="path"/> writes it ('git --attr-source=&lt;commit&gt; cat-file --filters --path=&lt;path&gt; &lt;blob&gt;'): attributes from the commit, core.autocrlf from the repo's config.
    /// A git without --attr-source (exit 129) is asked again without it, so the working tree's attributes decide; the result says so.
    /// </summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">The commit whose .gitattributes apply.</param>
    /// <param name="path">The path from the repo root; no leading '-', no control character.</param>
    /// <param name="blob">The blob sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The bytes, NotFound (SND-CONTEXT-003) when the repo has no such blob, or SND-CONTEXT-010.</returns>
    public async ValueTask<Result<CheckoutRead>> CheckoutBytes(string repo, string commit, string path, string blob, CancellationToken cancellationToken)
    {
        if (!IsSha(commit) || !PathOk(path))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha or a path git can be asked for");
        }

        var exists = await Exists(repo, blob, cancellationToken).ConfigureAwait(false);
        if (exists.IsError)
        {
            return exists.Error;
        }

        var withSource = await RunCatFile(repo, [$"--attr-source={commit}", "cat-file", "--filters", $"--path={path}", blob], cancellationToken).ConfigureAwait(false);
        if (withSource.IsError || withSource.Value.Exit != UsageExit)
        {
            return Read(withSource, workingTreeAttributes: false);
        }

        var plain = await RunCatFile(repo, ["cat-file", "--filters", $"--path={path}", blob], cancellationToken).ConfigureAwait(false);
        return Read(plain, workingTreeAttributes: true);
    }

    private static bool PathOk(string path) => path.Length > 0 && path[0] != '-' && !path.Any(char.IsControl);

    private static Result<CheckoutRead> Read(Result<(int Exit, byte[] Stdout)> run, bool workingTreeAttributes)
    {
        if (run.IsError)
        {
            return run.Error;
        }

        return run.Value.Exit == 0 ? new CheckoutRead(run.Value.Stdout, workingTreeAttributes) : Failed("cat-file", run.Value.Exit);
    }

    private async ValueTask<Result<(int Exit, byte[] Stdout)>> RunCatFile(string repo, string[] args, CancellationToken cancellationToken)
    {
        var start = CleanGit(["-C", repo, "-c", "core.quotepath=off", .. args]);
        return await Execute(start, "cat-file", CopyBytes, cancellationToken).ConfigureAwait(false);
    }
}
