using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The IImageScaler contract: the fake and the Skia scaler both pass it.</summary>
public abstract class ImageScalerContract
{
    private static readonly JevLimits _limits = JevLimits.Default;

    /// <summary>The scaler under test.</summary>
    /// <returns>A fresh scaler.</returns>
    protected abstract IImageScaler Create();

    /// <summary>An image of this size and format that the scaler can decode.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="format">The format.</param>
    /// <returns>The attachment.</returns>
    protected abstract ImageAttachment Image(int width, int height, ImageFormat format);

    /// <summary>An image that fits comes back as the very same attachment: no decode, no re-encode.</summary>
    [Theory]
    [InlineData(1512, 982)]
    [InlineData(280, 280)]
    [InlineData(1568, 1568)]
    public void A_fitting_image_comes_back_untouched(int width, int height)
    {
        var image = Image(width, height, ImageFormat.Png);

        Expect.That(ReferenceEquals(Create().Fit(image, _limits), image), "the same attachment");
    }

    /// <summary>A retina frame shrinks by one factor with both sides rounded down, in its own format.</summary>
    [Theory]
    [InlineData(3024, 1964, ImageFormat.Png, 1568, 1018)]
    [InlineData(3024, 1964, ImageFormat.Jpeg, 1568, 1018)]
    [InlineData(1600, 1600, ImageFormat.Png, 1568, 1568)]
    public void A_big_image_shrinks_to_fit_in_its_own_format(int width, int height, ImageFormat format, int fitWidth, int fitHeight)
    {
        var fitted = Create().Fit(Image(width, height, format), _limits);

        Expect.That(fitted is not null, "it fits after scaling");
        Expect.That(fitted!.Width == fitWidth && fitted.Height == fitHeight && fitted.Format == format, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{fitWidth}x{fitHeight} in {format}"));
        Expect.That((long)fitted.Width * fitted.Height <= _limits.MaxPixels, "within the pixel limit");
    }

    /// <summary>An image that can't fit both limits at its aspect ratio, or is already too small, can't be fitted (nothing is upscaled).</summary>
    [Theory]
    [InlineData(4000, 500)]
    [InlineData(200, 120)]
    [InlineData(279, 1000)]
    public void An_image_that_cant_fit_comes_back_null(int width, int height) =>
        Expect.That(Create().Fit(Image(width, height, ImageFormat.Png), _limits) is null, "null");
}
