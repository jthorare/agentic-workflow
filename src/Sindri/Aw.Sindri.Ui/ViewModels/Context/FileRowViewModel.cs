using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One row of the Files view: a folder or a file with its status, the features it is for and its tickets and decisions.</summary>
internal sealed class FileRowViewModel
{
    private const int FeaturesShown = 2;
    private const int TagsShown = 5;

    /// <summary>The row for one entry of the file map.</summary>
    /// <param name="entry">The entry as served.</param>
    /// <param name="symbols">The symbols column of the folder, or null for none.</param>
    internal FileRowViewModel(FileMapEntry entry, SymbolsColumnViewModel? symbols = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Name = entry.Name;
        Path = entry.Path;
        IsDirectory = string.Equals(entry.Type, "dir", StringComparison.Ordinal);
        IsSuggested = string.Equals(entry.Status, "suggested", StringComparison.Ordinal);
        StatusWord = Labels.ContextFilesStatus(entry.Status);
        FilesText = Labels.ContextFilesCount(entry.Files);
        ForText = For(entry.Features);
        IntentText = entry.Intent is { } intent ? Labels.ContextFilesIntent(intent.Records, intent.Decisions) : Labels.ContextFilesIntentNone;
        SymbolsText = IsDirectory ? string.Empty : symbols?.Cell(Path) ?? string.Empty;
        SymbolsOpacity = symbols?.Dimmed == true ? MapHighlightStyle.DimmedOpacity : 1;
        AutomationName = Dots(Name, StatusWord, ForText, IntentText, SymbolsText);
    }

    private FileRowViewModel()
    {
        Name = Labels.ContextFilesLoading;
        Path = string.Empty;
        StatusWord = string.Empty;
        FilesText = string.Empty;
        ForText = string.Empty;
        IntentText = string.Empty;
        SymbolsText = string.Empty;
        SymbolsOpacity = 1;
        AutomationName = Name;
    }

    /// <summary>The stand-in for a row whose page is not read yet: it says it is reading and opens nothing.</summary>
    /// <returns>A new placeholder row.</returns>
    internal static FileRowViewModel Placeholder() => new();

    /// <summary>The name, as served.</summary>
    public string Name { get; }

    /// <summary>The path, as served (a folder's ends in a slash).</summary>
    public string Path { get; }

    /// <summary>Whether it is a folder (it opens).</summary>
    public bool IsDirectory { get; }

    /// <summary>Whether it is a file (it does not open).</summary>
    public bool IsFile => !IsDirectory;

    /// <summary>Whether the tool suggested its link and nobody confirmed it: the row is hatched.</summary>
    public bool IsSuggested { get; }

    /// <summary>The status in words.</summary>
    public string StatusWord { get; }

    /// <summary>How many files a folder holds.</summary>
    public string FilesText { get; }

    /// <summary>What it is for: its features, a suggested one with how sure the tool is.</summary>
    public string ForText { get; }

    /// <summary>Its tickets and decisions.</summary>
    public string IntentText { get; }

    /// <summary>The names the code index gave for the file, or why it cannot (empty for a folder and for a file the index does not know).</summary>
    public string SymbolsText { get; }

    /// <summary>1, or the dimmed opacity when the index is stale.</summary>
    public double SymbolsOpacity { get; }

    /// <summary>The row's accessible name: everything the row says, in order.</summary>
    public string AutomationName { get; }

    private static string For(IReadOnlyList<FileFeature> features)
    {
        if (features.Count == 0)
        {
            return Labels.ContextFilesNoFeature;
        }

        var homes = features.Where(f => !IsTag(f)).ToList();
        var tags = features.Where(IsTag).Take(TagsShown).Select(t => t.Key[(t.Key.LastIndexOf(':') + 1)..]).ToList();
        var shown = homes.Take(FeaturesShown).Select(Feature);
        var more = homes.Count > FeaturesShown ? "+" + (homes.Count - FeaturesShown).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        var home = homes.Count == 0 ? Labels.ContextFilesNoHome : Dots(string.Join(", ", shown), more);
        return Dots(home, tags.Count == 0 ? null : Labels.ContextFilesAlsoTagged(string.Join(", ", tags)));
    }

    // The model's cross-cutting names for a file come after its home and are marked as tags (spec 2026-10-27 §2.3).
    private static bool IsTag(FileFeature feature) => string.Equals(feature.Edge, "tags", StringComparison.Ordinal);

    private static string Feature(FileFeature feature)
    {
        var name = Labels.ContextFilesFeature(feature.Key);
        return !feature.Confirmed && feature.Confidence is { } confidence
            ? name + " (" + Labels.ContextMapSuggestedPct((int)Math.Round(confidence * 100, MidpointRounding.AwayFromZero)) + ")"
            : name;
    }

    private static string Dots(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
}
