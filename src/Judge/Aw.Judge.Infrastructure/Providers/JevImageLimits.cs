using Aw.Judge.Core.Images;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>What Jev takes of an image (its docs: rejected, not resized): 280 to 1568 px a side, 78,400 to 2,458,624 pixels.</summary>
internal static class JevImageLimits
{
    private const int MinSide = 280;

    private const int MaxSide = 1568;

    private const long MinPixels = 78_400;

    private const long MaxPixels = 2_458_624;

    /// <summary>Whether the image is within Jev's size limits (the 16 MiB file limit is Jev's to report, as a 413).</summary>
    /// <param name="image">The image.</param>
    /// <returns>True when it can be sent as it is.</returns>
    public static bool Fits(ImageAttachment image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var pixels = (long)image.Width * image.Height;
        return image.Width is >= MinSide and <= MaxSide
            && image.Height is >= MinSide and <= MaxSide
            && pixels is >= MinPixels and <= MaxPixels;
    }
}
