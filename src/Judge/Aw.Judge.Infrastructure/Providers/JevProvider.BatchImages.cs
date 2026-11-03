using System.Diagnostics;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A batch with images (the prompt sorter's and trait-check's): the best-effort rules of a hook's single call, applied to the batch.</summary>
internal sealed partial class JevProvider
{
    // Under ImagesBestEffort an image Jev can't take is dropped and noted, and the batch goes with the rest, or as text when none are left; a 413 or a 400 on
    // the image batch is asked once more as text when at least half the budget is left, else the batch is unanswered (the hook fails open as on a timeout).
    // A batch has no CLI to hand the images to, so without the flag an image that can't be sent is unavailable as in Decide.
    private async ValueTask<ProviderResult> BatchWithImages(BatchRequest batch, int inputCapChars, CancellationToken cancellationToken)
    {
        if (!batch.ImagesBestEffort)
        {
            return await BatchOnce(batch, inputCapChars, cancellationToken).ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        var (kept, note) = Partition(batch.Images);
        List<string> notes = note is null ? [] : [note];
        var result = await BatchOnce(batch with { Images = kept }, inputCapChars, cancellationToken).ConfigureAwait(false);
        if (kept.Count > 0 && Retry(result) is { } cause)
        {
            var left = batch.Budget - Stopwatch.GetElapsedTime(started);
            if (left * 2 < batch.Budget)
            {
                notes.Add($"{cause}; not enough of the budget was left to ask jev again");
                result = new ProviderErrored(ProviderReasons.Http(cause.Contains("(413)", StringComparison.Ordinal) ? 413 : 400));
            }
            else
            {
                notes.Add($"{cause}; asked jev again as text");
                result = await BatchOnce(batch with { Images = [], Budget = left }, inputCapChars, cancellationToken).ConfigureAwait(false);
            }
        }

        return notes.Count == 0 ? result : result with { ImageNotes = notes };
    }
}
