using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Core.Profile;

/// <summary>The line's profile values with their defaults (line spec Appendix D): what a key left out means.</summary>
public static class LineProfile
{
    private static readonly string[] _sizes = ["XS", "S", "M", "L", "XL"];

    /// <summary>The defaults of each size class: files, items, then the budget's tokens, turns and wall minutes. Q5 (XS 3 files and 2 items, S 8 and 4, M 20 and 8) is the spec's; L and XL (refused as phase sizes) and every budget are provisional, tuned after LN-P from the merge records.</summary>
    private static readonly Dictionary<string, (int Files, int Items, long Tokens, int Turns, int WallMinutes)> _defaults = new(StringComparer.Ordinal)
    {
        ["XS"] = (3, 2, 100_000, 30, 20),
        ["S"] = (8, 4, 400_000, 60, 45),
        ["M"] = (20, 8, 800_000, 100, 90),
        ["L"] = (40, 16, 1_600_000, 150, 180),
        ["XL"] = (80, 32, 3_200_000, 200, 360),
    };

    /// <summary>workflow.line.parallel: 2.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static int Parallel(SindriProfile profile) => Of(profile).Line?.Parallel ?? 2;

    /// <summary>workflow.line.maxOpenPrs: 4.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static int MaxOpenPrs(SindriProfile profile) => Of(profile).Line?.MaxOpenPrs ?? 4;

    /// <summary>workflow.line.maxStack: 4.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static int MaxStack(SindriProfile profile) => Of(profile).Line?.MaxStack ?? 4;

    /// <summary>workflow.plan.maxPhaseSize: M.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static string MaxPhaseSize(SindriProfile profile) => Of(profile).Plan?.MaxPhaseSize ?? "M";

    /// <summary>workflow.prove.holdoutMin: 2.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static int HoldoutMin(SindriProfile profile) => Of(profile).Prove?.HoldoutMin ?? 2;

    /// <summary>workflow.steps.longMax: 2h.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static string LongMax(SindriProfile profile) => Of(profile).Steps?.LongMax ?? "2h";

    /// <summary>workflow.docs.globs: docs/** and *.md.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The globs.</returns>
    public static IReadOnlyList<string> DocsGlobs(SindriProfile profile) => Of(profile).Docs?.Globs ?? ["docs/**", "*.md"];

    /// <summary>workflow.ship.draft: false.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The value.</returns>
    public static bool Draft(SindriProfile profile) => Of(profile).Ship?.Draft ?? false;

    /// <summary>The sizes file aw-packets reads for PKT-SIZE-001 (LN-PKT3): the five size classes from workflow.plan.sizes, a size or a field left out taking its default (XS 3 files and 2 items, S 8 and 4, M 20 and 8, spec Q5).</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The sizes file's JSON text.</returns>
    public static string SizesFile(SindriProfile profile)
    {
        var overrides = Of(profile).Plan?.Sizes ?? new Dictionary<string, SizeSettings>(StringComparer.Ordinal);
        var sizes = new JsonObject();
        foreach (var size in _sizes)
        {
            sizes[size] = SizeEntry(_defaults[size], overrides.GetValueOrDefault(size));
        }

        return LineJson.Write(new JsonObject { ["schemaVersion"] = 1, ["sizes"] = sizes });
    }

    private static JsonObject SizeEntry((int Files, int Items, long Tokens, int Turns, int WallMinutes) fallback, SizeSettings? own) => new()
    {
        ["files"] = own?.Files ?? fallback.Files,
        ["items"] = own?.Items ?? fallback.Items,
        ["budget"] = BudgetEntry(fallback, own?.Budget),
    };

    private static JsonObject BudgetEntry((int Files, int Items, long Tokens, int Turns, int WallMinutes) fallback, BudgetSettings? own) =>
        new() { ["tokens"] = own?.Tokens ?? fallback.Tokens, ["turns"] = own?.Turns ?? fallback.Turns, ["wallMinutes"] = own?.WallMinutes ?? fallback.WallMinutes };

    private static WorkflowSettings Of(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Workflow ?? new WorkflowSettings();
    }
}
