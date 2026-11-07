using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.GetReviewPage;

/// <summary>
/// The forge side of the PR page (spec §6.2 "One head"): the head first, then the files and threads, then the head again. A head that moved in between is read once more;
/// after a second move the newest reads come back marked as moving. A head read that fails is the call's error; a files or threads read that fails is that panel's.
/// </summary>
/// <param name="forge">The forge port.</param>
internal sealed class ReviewPageReader(IPullRequests forge)
{
    private const int MaxHeadReads = 2;

    internal async ValueTask<Result<ReviewPageReads>> Read(string link, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxHeadReads; attempt++)
        {
            var before = await forge.Get(link, cancellationToken).ConfigureAwait(false);
            if (before.IsError)
            {
                return before.Error;
            }

            var files = forge.Files(link, cancellationToken).AsTask();
            var threads = forge.Threads(link, cancellationToken).AsTask();
            await Task.WhenAll(files, threads).ConfigureAwait(false);
            var after = await forge.Get(link, cancellationToken).ConfigureAwait(false);
            if (after.IsError)
            {
                return after.Error;
            }

            var moved = !string.Equals(before.Value.HeadSha, after.Value.HeadSha, StringComparison.OrdinalIgnoreCase);
            if (!moved || attempt == MaxHeadReads)
            {
                return new ReviewPageReads(after.Value, Wrap(await files.ConfigureAwait(false), new PullFiles(Files: [], CapReached: false)), Wrap(await threads.ConfigureAwait(false), empty: []), moved);
            }
        }

        return SindriErrors.ServiceFailed.With("the page read ended without a result");
    }

    private static ReviewPageRead<T> Wrap<T>(Result<T> result, T empty) => result.IsSuccess ? new ReviewPageRead<T>(result.Value) : new ReviewPageRead<T>(empty, result.Error.Message);
}
