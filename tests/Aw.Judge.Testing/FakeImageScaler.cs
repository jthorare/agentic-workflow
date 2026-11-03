using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Testing;

/// <summary>
/// The IImageScaler fake: the same fit as the real scaler, worked out from the sizes alone. An image that fits comes back as it is; one that
/// doesn't comes back with the new size and its own bytes (nothing is resampled). Passes ImageScalerContract.
/// </summary>
public sealed class FakeImageScaler : IImageScaler
{
    private readonly List<ImageAttachment> _fitted = [];

    /// <summary>Every image asked about, in order.</summary>
    public IReadOnlyList<ImageAttachment> Fitted => _fitted;

    /// <inheritdoc />
    public ImageAttachment? Fit(ImageAttachment image, JevLimits limits)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(limits);
        _fitted.Add(image);
        var pixels = (long)image.Width * image.Height;
        if (image.Width <= limits.MaxSide && image.Height <= limits.MaxSide && pixels <= limits.MaxPixels && pixels >= limits.MinPixels
            && image.Width >= limits.MinSide && image.Height >= limits.MinSide)
        {
            return image;
        }

        var factor = Math.Min(1.0, Math.Min(Math.Min((double)limits.MaxSide / image.Width, (double)limits.MaxSide / image.Height), Math.Sqrt((double)limits.MaxPixels / pixels)));
        var width = (int)Math.Floor((image.Width * factor) + 1e-6);
        var height = (int)Math.Floor((image.Height * factor) + 1e-6);
        return Math.Min(width, height) < limits.MinSide ? null : image with { Width = width, Height = height };
    }
}
