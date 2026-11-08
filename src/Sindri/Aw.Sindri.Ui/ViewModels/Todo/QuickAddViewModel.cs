using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The quick-add box (todo spec §3.2): one field, a line under it saying what will be saved, Enter saves, Esc drops it.</summary>
internal sealed class QuickAddViewModel : ObservableObject
{
    internal QuickAddViewModel(ISindriApi api, Func<Uri?> url, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(clock);
    }

    /// <summary>The box's accessible name and its field's.</summary>
    public string Name { get; } = Labels.QuickAddName;

    /// <summary>The field's placeholder.</summary>
    public string Placeholder { get; } = Labels.QuickAddPlaceholder;

    /// <summary>True while the box is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The typed text.</summary>
    public string Text { get; set => Set(ref field, value ?? string.Empty); } = string.Empty;

    /// <summary>The line under the field: "Due Fri 11-13", "No date" or "Type a todo first".</summary>
    public string Preview { get; private set => Set(ref field, value); } = Labels.QuickAddNoDate;

    /// <summary>Why the last save failed; empty otherwise.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>True while a save waits for its answer.</summary>
    public bool IsSaving { get; private set => Set(ref field, value); }

    /// <summary>Opens the box with an empty field (an open box stays as it is).</summary>
    internal void Open() => throw new NotSupportedException();

    /// <summary>Closes the box and drops the text.</summary>
    internal void Close() => throw new NotSupportedException();

    /// <summary>Enter: saves the todo, or says why not.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Save(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = TodoQuickParse.Parse(Text, default);
        throw new NotSupportedException();
    }
}
