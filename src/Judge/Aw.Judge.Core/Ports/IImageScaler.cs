using Aw.Judge.Core.Images;

namespace Aw.Judge.Core.Ports;

/// <summary>Shrinks an image that is too big for Jev so it can be sent (Jev rejects, never resizes).</summary>
public interface IImageScaler
{
    /// <summary>
    /// Fits the image to <paramref name="limits"/> with one scale factor, min(maxSide / width, maxSide / height, sqrt(maxPixels / (width x height)), 1),
    /// both sides rounded down and the aspect ratio kept, in the image's own format. An image that already fits comes back as it is (the same
    /// instance, no re-encode).
    /// </summary>
    /// <param name="image">The image as read.</param>
    /// <param name="limits">What Jev takes.</param>
    /// <returns>The image to send, or null when it can't be fitted: the short side would be under the minimum, or the scaler can't decode it.</returns>
    public ImageAttachment? Fit(ImageAttachment image, JevLimits limits);
}
