using System.Globalization;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Images;
using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>One screen's frames compared: masked, cropped to what both frames cover, and measured.</summary>
/// <param name="App">The app's frame as compared.</param>
/// <param name="Design">The design's frame as compared.</param>
/// <param name="Pixels">The pixel comparison.</param>
/// <param name="Problem">Set when the two frames are not the same size; null otherwise.</param>
public sealed record ScreenComparison(RgbaImage App, RgbaImage Design, PixelComparison Pixels, string? Problem)
{
    /// <summary>Compares an app frame with its design frame.</summary>
    /// <param name="app">The app's frame.</param>
    /// <param name="design">The design's frame.</param>
    /// <param name="masks">The regions to paint over in both before comparing.</param>
    /// <returns>The comparison, or the pixel compare's error.</returns>
    public static Result<ScreenComparison> Compare(RgbaImage app, RgbaImage design, IReadOnlyList<Region> masks)
    {
        var width = Math.Min(app.Width, design.Width);
        var height = Math.Min(app.Height, design.Height);
        var common = new Region(0, 0, width, height);
        var maskedApp = ImageOps.Crop(ImageMasks.Apply(app, masks), common);
        var maskedDesign = ImageOps.Crop(ImageMasks.Apply(design, masks), common);
        var compared = maskedApp.Bind(a => maskedDesign.Bind(d => PixelMatch.Compare(d, a, PixelMatchOptions.Default).Map(p => new ScreenComparison(a, d, p, Problem: null))));
        if (compared.IsError || (app.Width == design.Width && app.Height == design.Height))
        {
            return compared;
        }

        return compared.Value with
        {
            Problem = string.Create(CultureInfo.InvariantCulture, $"the frames differ in size: app {app.Width}x{app.Height}, design {design.Width}x{design.Height}"),
        };
    }

    /// <summary>True when the frames are the same size and the difference is within <paramref name="thresholdPercent"/>.</summary>
    /// <param name="thresholdPercent">The limit, in percent.</param>
    /// <returns>Whether the screen passes.</returns>
    public bool Within(double thresholdPercent) => Problem is null && Pixels.DiffPercent <= thresholdPercent;
}
