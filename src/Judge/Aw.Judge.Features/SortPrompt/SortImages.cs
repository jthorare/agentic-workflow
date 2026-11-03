using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Features.SortPrompt;

/// <summary>Reads the images a prompt-sort hook sent along, best effort: each path once, the first 4; one that can't be read is dropped with a line saying why.</summary>
internal static class SortImages
{
    private const int MaxImages = 4;

    /// <summary>Reads the paths.</summary>
    /// <param name="reader">The image reader.</param>
    /// <param name="paths">The absolute paths, newest first.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The images read, and a line for each path left out.</returns>
    internal static async ValueTask<(IReadOnlyList<ImageAttachment> Images, IReadOnlyList<string> Dropped)> Read(IImageReader reader, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        List<ImageAttachment> images = [];
        List<string> dropped = [];
        for (var i = 0; i < paths.Count; i++)
        {
            if (i >= MaxImages)
            {
                dropped.Add($"{paths[i]} is past the {MaxImages} images a hook sends; asked jev without it");
                continue;
            }

            var read = await reader.Read([paths[i]], root: null, cancellationToken).ConfigureAwait(false);
            if (read.Problem is not null || read.Images.Count != 1)
            {
                dropped.Add($"{read.Problem ?? (paths[i] + " could not be read")}; asked jev without it");
                continue;
            }

            images.Add(read.Images[0]);
        }

        return (images, dropped);
    }
}
