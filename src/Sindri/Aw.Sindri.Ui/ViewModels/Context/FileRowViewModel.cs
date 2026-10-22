using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One row of the Files view: a folder or a file with its status, the features it is for and its tickets and decisions.</summary>
internal sealed class FileRowViewModel
{
    private const int FeaturesShown = 2;

    /// <summary>The row for one entry of the file map.</summary>
    /// <param name="entry">The entry as served.</param>
    internal FileRowViewModel(FileMapEntry entry)
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
        AutomationName = Dots(Name, StatusWord, ForText, IntentText);
    }

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

    /// <summary>The row's accessible name: everything the row says, in order.</summary>
    public string AutomationName { get; }

    private static string For(IReadOnlyList<FileFeature> features)
    {
        if (features.Count == 0)
        {
            return Labels.ContextFilesNoFeature;
        }

        var shown = features.Take(FeaturesShown).Select(Feature);
        var more = features.Count > FeaturesShown ? "+" + (features.Count - FeaturesShown).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        return Dots(string.Join(", ", shown), more);
    }

    private static string Feature(FileFeature feature)
    {
        var name = Labels.ContextFilesFeature(feature.Key);
        return !feature.Confirmed && feature.Confidence is { } confidence
            ? name + " (" + Labels.ContextMapSuggestedPct((int)Math.Round(confidence * 100, MidpointRounding.AwayFromZero)) + ")"
            : name;
    }

    private static string Dots(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
}
