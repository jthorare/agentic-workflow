using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One required check in the editor: id, title, the globs that make it apply and what decides it, written as "ci: ui-tests".</summary>
/// <param name="index">The row's place, from 1, for the screen-reader names.</param>
/// <param name="id">The check's id.</param>
/// <param name="title">What it says in the checks panel.</param>
/// <param name="whenText">The globs, one per line; empty means always.</param>
/// <param name="needText">What decides it.</param>
/// <param name="need">The need as the API wrote it, kept while the text is left as it was.</param>
/// <param name="originalNeedText">The need text as it was read, when this row is a copy of an edited one; null means <paramref name="needText"/>.</param>
internal sealed class SettingsCheckRow(int index, string id, string title, string whenText, string needText, JsonElement? need, string? originalNeedText = null) : ObservableObject
{
    /// <summary>The row's place, from 1.</summary>
    public int Index { get; } = index;

    /// <summary>The check's id.</summary>
    public string Id { get; set => Set(ref field, value); } = id;

    /// <summary>What it says in the checks panel.</summary>
    public string Title { get; set => Set(ref field, value); } = title;

    /// <summary>The globs that make it apply, one per line; empty means always.</summary>
    public string WhenText { get; set => Set(ref field, value); } = whenText;

    /// <summary>What decides it, like "ci: ui-tests".</summary>
    public string NeedText { get; set => Set(ref field, value); } = needText;

    /// <summary>The need as the API wrote it, or null for a new row.</summary>
    internal JsonElement? Need { get; } = need;

    /// <summary>True for a check that runs a command: its need shows but does not edit.</summary>
    public bool NeedReadOnly { get; } = ReviewSettingsNeed.IsCommand(need);

    /// <summary>The need text as it was when the row was read.</summary>
    internal string OriginalNeedText { get; } = originalNeedText ?? needText;

    /// <summary>The id field's name.</summary>
    public string IdName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsCheckRow, Index, Labels.ReviewSettingsFieldId);

    /// <summary>The title field's name.</summary>
    public string TitleName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsCheckRow, Index, Labels.ReviewSettingsFieldTitle);

    /// <summary>The when field's name.</summary>
    public string WhenName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsCheckRow, Index, Labels.ReviewSettingsFieldWhen);

    /// <summary>The need field's name.</summary>
    public string NeedName => Labels.ReviewSettingsRowField(Labels.ReviewSettingsCheckRow, Index, Labels.ReviewSettingsFieldNeed);

    /// <summary>The remove button's name.</summary>
    public string RemoveName => Labels.ReviewSettingsRemoveRow(Labels.ReviewSettingsCheckRow, Index);

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The remove button's text.</summary>
    public string RemoveText => Labels.ReviewSettingsRemove;
#pragma warning restore CA1822, MA0041

    /// <summary>The same row at another place.</summary>
    /// <param name="place">The new place, from 1.</param>
    /// <returns>The copy.</returns>
    internal SettingsCheckRow At(int place) => new(place, Id, Title, WhenText, NeedText, Need, OriginalNeedText);
}
