namespace Aw.Evidence.Core.Desktop;

/// <summary>The images of one screen at one size.</summary>
/// <param name="App">The app's frame.</param>
/// <param name="Design">The mockup's frame.</param>
/// <param name="Diff">The pixel diff.</param>
/// <param name="SideBySide">Design, app and diff in one image.</param>
public sealed record DesktopFiles(string App, string Design, string Diff, string SideBySide)
{
    /// <summary>The images of a screen at a size, below the run directory.</summary>
    /// <param name="runDirectory">The run directory (absolute).</param>
    /// <param name="name">The screen.</param>
    /// <param name="size">The window size, 'WxH'.</param>
    /// <returns>The paths.</returns>
    public static DesktopFiles For(string runDirectory, string name, string size)
    {
        var folder = runDirectory.TrimEnd('/') + "/" + name + "-" + size + "/";
        return new DesktopFiles(folder + "app.png", folder + "design.png", folder + "diff.png", folder + "side-by-side.png");
    }
}
