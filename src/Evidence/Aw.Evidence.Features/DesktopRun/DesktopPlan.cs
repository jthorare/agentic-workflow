using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>What one run drives and compares: the config, the manifest's screens and the folder the mockups are in.</summary>
/// <param name="Repo">The repository (absolute).</param>
/// <param name="RunDirectory">The run directory (absolute).</param>
/// <param name="DesignDirectory">The mockups' folder (absolute).</param>
/// <param name="Settings">The repo's desktop config.</param>
/// <param name="Threshold">The default limit of differing pixels, in percent.</param>
/// <param name="Screens">The screens to run, in manifest order.</param>
internal sealed record DesktopPlan(string Repo, string RunDirectory, string DesignDirectory, ResolvedDesktop Settings, double Threshold, IReadOnlyList<DesktopScreen> Screens)
{
    /// <summary>Every size some screen is captured at, in first-use order.</summary>
    internal IReadOnlyList<ViewportSize> Sizes => [.. Screens.SelectMany(SizesOf).Distinct()];

    /// <summary>The screens captured at <paramref name="size"/>.</summary>
    internal IEnumerable<DesktopScreen> At(ViewportSize size) => Screens.Where(s => SizesOf(s).Contains(size));

    internal IReadOnlyList<ViewportSize> SizesOf(DesktopScreen screen) =>
        screen.Sizes is { Count: > 0 } named ? [.. named.Select(static s => SizeText.TryParse(s, out var size) ? size : new ViewportSize(0, 0))] : Settings.Sizes;

    internal double ThresholdOf(DesktopScreen screen) => screen.Threshold ?? Threshold;

    /// <summary>What names a screen at a size in the maps of one run.</summary>
    internal static string KeyOf(DesktopScreen screen, ViewportSize size) => screen.Name + "@" + size.Describe();

    internal DesktopFiles FilesOf(DesktopScreen screen, ViewportSize size) => DesktopFiles.For(RunDirectory, screen.Name, size.Describe());
}
