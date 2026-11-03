using Aw.Judge.Core.Images;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>What Jev takes of an image: the one set of limits, <see cref="JevLimits.Default"/>.</summary>
internal static class JevImageLimits
{
    /// <summary>Whether the image is within Jev's size limits as it is (the 16 MiB file limit is Jev's to report, as a 413).</summary>
    /// <param name="image">The image.</param>
    /// <returns>True when it can be sent without scaling.</returns>
    public static bool Fits(ImageAttachment image) => JevLimits.Default.Fits(image);
}
