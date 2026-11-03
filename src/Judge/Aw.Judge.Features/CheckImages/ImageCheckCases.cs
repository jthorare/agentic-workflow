using System.Security.Cryptography;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Features.CheckImages;

/// <summary>Serves the check's cases from memory, under one evidence directory, to the evaluator's image reader.</summary>
/// <param name="set">The cases.</param>
internal sealed class ImageCheckCases(IReadOnlyList<ImageCase> set) : IImageReader
{
    /// <summary>The evidence directory the questions name; nothing is written there (the CLIs get their own per-call copy).</summary>
    public const string Root = "/aw-check-images";

    /// <summary>The case as the reader would have made it from a file.</summary>
    /// <param name="item">The case.</param>
    /// <returns>The attachment.</returns>
    public static ImageAttachment Attachment(ImageCase item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var bytes = item.Bytes.ToArray();
        var header = ImageHeader.Read(bytes);
        return new ImageAttachment
        {
            Path = Root + "/" + item.Name + ".png",
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Bytes = bytes,
            Width = header?.Width ?? 0,
            Height = header?.Height ?? 0,
            Format = header?.Format ?? ImageFormat.Png,
        };
    }

    /// <inheritdoc />
    public ValueTask<ImageRead> Read(IReadOnlyList<string> paths, string? root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<ImageAttachment> images = [];
        foreach (var path in paths)
        {
            var found = set.FirstOrDefault(c => string.Equals(c.Name + ".png", path, StringComparison.Ordinal));
            if (found is null)
            {
                return ValueTask.FromResult(new ImageRead { Problem = $"image {path} is not one of the cases" });
            }

            images.Add(Attachment(found));
        }

        return ValueTask.FromResult(new ImageRead { Images = images });
    }
}
