using System.ComponentModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>
/// Create item (UI spec §4.5, §7): `C` opens a small form (title, repo, body) whose button is the catalog's CreateItem
/// control, so it is drawn pending until P3 ships and keeps the §4.1 lifecycle. The new row appears from the stream's
/// re-query, never from the answer. The form closes once the accepted outcome has shown.
/// </summary>
internal sealed class CreateItemViewModel(CatalogViewModel catalog, Func<Uri?> url, ISindriApi api) : ObservableObject
{
    private int _form;   // which opening of the form this is; an answer is for the one that sent it

    /// <summary>The app's copy for the form.</summary>
    public ItemText Text { get; } = ItemText.Instance;

    /// <summary>True while the form is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The title (required).</summary>
    public string Title
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSubmit));
            OnChanged(nameof(IsTitleBlank));
            if (!string.IsNullOrWhiteSpace(value))
            {
                ClearTitleError();
            }
        }
    } = string.Empty;

    /// <summary>True while the title is blank: the only state in which "Title is required" can be said, so the live region is in the tree from then on.</summary>
    public bool IsTitleBlank => string.IsNullOrWhiteSpace(Title);

    /// <summary>The repo, or empty for the API's default.</summary>
    public string Repo { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The body, or empty.</summary>
    public string Body { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The catalog's CreateItem control (null when the catalog has no such row).</summary>
    public ControlViewModel? Control { get; private set => Set(ref field, value); }

    /// <summary>
    /// True when a submit would send: the control can run, the title isn't blank and nothing was created yet (the 2 s the
    /// accepted outcome shows, a second press must not make a second item). The button itself stays enabled: a blank title
    /// is answered under the field (<see cref="TitleError"/>), not by a dead button.
    /// </summary>
    public bool CanSubmit => Control is { CanRun: true } && !string.IsNullOrWhiteSpace(Title) && CreatedId.Length == 0;

    /// <summary>"Title is required" after a blank submit, until a title is typed; else empty.</summary>
    public string TitleError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while <see cref="TitleError"/> shows.</summary>
    public bool HasTitleError { get; private set => Set(ref field, value); }

    /// <summary>True while the title's live region is live (its gate, set before its text changes).</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The title's live region text; never empty.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.TitleRequired;

    /// <summary>Counts the blank submits that ask the view to put focus on the title.</summary>
    public int TitleFocusRequests { get; private set => Set(ref field, value); }

    /// <summary>The new item's id once accepted, else empty.</summary>
    public string CreatedId
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSubmit));
        }
    } = string.Empty;

    /// <summary>`C`: opens the form with the catalog's control as it is now. The form is empty after a created item; after Esc or Cancel it keeps the draft.</summary>
    internal void Open()
    {
        if (Control is { } old)
        {
            old.PropertyChanged -= OnControl;
        }

        var form = ++_form;
        Control = catalog.On("CreateItem", string.Empty, ct => Send(form, ct));
        if (Control is { } control)
        {
            control.PropertyChanged += OnControl;
            control.Precondition = Ready;
        }

        if (CreatedId.Length > 0)
        {
            (Title, Repo, Body, CreatedId) = (string.Empty, string.Empty, string.Empty, string.Empty);
        }

        ClearTitleError();
        IsOpen = true;
        OnChanged(nameof(CanSubmit));
    }

    /// <summary>Esc or Cancel: closes the form; nothing is sent.</summary>
    internal void Close() => IsOpen = false;

    /// <summary>Sends the form once through the control.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Submit(CancellationToken cancellationToken) =>
        Control is { } control ? control.Press(cancellationToken) : Task.CompletedTask;

    // The control's precondition, for every way a press reaches it (the button, Enter in the title): nothing is sent twice, and
    // a blank title is said under the field, spoken once, and focus goes to the title.
    private bool Ready()
    {
        if (CreatedId.Length > 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Title))
        {
            return true;
        }

        RequireTitle();
        return false;
    }

    private void RequireTitle()
    {
        (TitleError, HasTitleError) = (Labels.TitleIsRequired, true);
        Say(Labels.TitleIsRequired);
        TitleFocusRequests++;
    }

    private void ClearTitleError()
    {
        if (HasTitleError)
        {
            (HasTitleError, TitleError) = (false, string.Empty);
        }

        IsAnnouncing = false;   // the region goes dark first; its text stays (never empty) and is not spoken
    }

    // Says a text through the polite region: dark first, the resting text set, then live, then the text (the flag changes
    // before the text, as the shell's regions do), so the resting text is never spoken and a text is spoken once.
    private void Say(string text)
    {
        if (IsAnnouncing && string.Equals(Announcement, text, StringComparison.Ordinal))
        {
            IsAnnouncing = false;
        }

        if (!IsAnnouncing)
        {
            Announcement = Labels.TitleRequired;
            IsAnnouncing = true;
        }

        Announcement = text;
    }

    private async Task<Result<CommandAcceptedView>> Send(int form, CancellationToken cancellationToken)
    {
        if (url() is not { } api1)
        {
            return SindriUiErrors.NotRunning.With("no API to send to");
        }

        var request = new CreateItemRequest(Title.Trim(), Blank(Repo), Blank(Body), Link: null);
        var created = await api.CreateItem(api1, request, cancellationToken).ConfigureAwait(true);
        if (created.IsError)
        {
            return created.Error;
        }

        if (form == _form)
        {
            CreatedId = created.Value.ItemId;   // only the form that sent it: Esc and C made another since
        }

        return created.Value.Accepted;
    }

    private static string? Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // The accepted outcome shows for its 2 s; then the form closes and empties (§4.1 lifecycle).
    private void OnControl(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControlViewModel.CanRun))
        {
            OnChanged(nameof(CanSubmit));
        }

        if (e.PropertyName == nameof(ControlViewModel.Phase) && sender is ControlViewModel { Phase: CommandPhase.Idle } && CreatedId.Length > 0 && IsOpen)
        {
            (Title, Repo, Body) = (string.Empty, string.Empty, string.Empty);
            IsOpen = false;
        }
    }
}
