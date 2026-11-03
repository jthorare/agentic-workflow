namespace Aw.Judge.Core.Images;

/// <summary>What Jev takes of an image (its docs: rejected, not resized).</summary>
/// <param name="MinSide">The least width or height, in pixels.</param>
/// <param name="MaxSide">The most width or height, in pixels.</param>
/// <param name="MinPixels">The least width times height.</param>
/// <param name="MaxPixels">The most width times height.</param>
public sealed record JevLimits(int MinSide, int MaxSide, long MinPixels, long MaxPixels)
{
    /// <summary>Jev's limits: 280 to 1568 px a side, 78,400 to 2,458,624 pixels.</summary>
    public static JevLimits Default { get; } = new(MinSide: 280, MaxSide: 1568, MinPixels: 78_400, MaxPixels: 2_458_624);

    /// <summary>Whether an image is within the limits as it is (the 16 MiB file limit is Jev's to report, as a 413).</summary>
    /// <param name="image">The image.</param>
    /// <returns>True when it can be sent without scaling.</returns>
    public bool Fits(ImageAttachment image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var pixels = (long)image.Width * image.Height;
        return image.Width >= MinSide && image.Width <= MaxSide && image.Height >= MinSide && image.Height <= MaxSide
            && pixels >= MinPixels && pixels <= MaxPixels;
    }
}
