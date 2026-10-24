using System.Text.RegularExpressions;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Desktop;

/// <summary>The checks of one manifest screen.</summary>
internal static partial class DesktopScreenRules
{
    internal static void Check(DesktopScreen screen, Problems problems)
    {
        var at = $"$.screens[{screen.Name}]";
        problems.AddIf(string.IsNullOrEmpty(screen.Name) || !ScreenName.IsMatch(screen.Name), $"{at}.name: lowercase letters, digits and '-', at most 64");
        problems.AddIf(!DesktopTargetsRules.IsRepoRelative(screen.Design) || !screen.Design.EndsWith(".html", StringComparison.Ordinal), $"{at}.design: must be an .html file below the design root");
        problems.AddIf(screen.Threshold is < 0 or > 100, $"{at}.threshold: must be 0 to 100");
        problems.AddIf(screen.SettleMs is < 0 or > 30_000, $"{at}.settleMs: must be 0 to 30000");
        problems.AddIf(screen.Fixture is { } steps && steps.Any(static s => s is null || s.Count == 0 || s.Any(string.IsNullOrEmpty)), $"{at}.fixture: each step must list its arguments, none empty");
        problems.AddIf(screen.Accepted is { } accepted && accepted.Any(string.IsNullOrWhiteSpace), $"{at}.accepted: an entry must not be empty");
        CheckSizes(screen, at, problems);
        CheckMasks(screen, at, problems);
        DesktopStepRules.Check(screen.Steps, at, problems);
    }

    private static void CheckSizes(DesktopScreen screen, string at, Problems problems)
    {
        foreach (var size in screen.Sizes ?? [])
        {
            problems.AddIf(!SizeText.TryParse(size, out _), $"{at}.sizes: '{size}' is not a size like 1440x900");
        }
    }

    private static void CheckMasks(DesktopScreen screen, string at, Problems problems)
    {
        foreach (var mask in screen.Masks ?? [])
        {
            problems.AddIf(mask is null || IsEmptyRegion(mask), $"{at}.masks: a mask needs x, y of 0 or more and w, h of 1 or more");
            problems.AddIf(mask is not null && string.IsNullOrWhiteSpace(mask.Why), $"{at}.masks: a mask needs a 'why'");
            problems.AddIf(mask?.Size is { } size && !SizeText.TryParse(size, out _), $"{at}.masks: size '{mask?.Size}' is not a size like 1440x900");
        }
    }

    private static bool IsEmptyRegion(ScreenMask mask) => mask.W < 1 || mask.H < 1 || mask.X < 0 || mask.Y < 0;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ScreenName { get; }
}
