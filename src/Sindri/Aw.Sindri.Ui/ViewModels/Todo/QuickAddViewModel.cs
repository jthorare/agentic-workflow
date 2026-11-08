using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The quick-add box (todo spec §3.2, §3.4): one field and a line under it saying what will be saved. Enter saves through the API and
/// closes the box; a failed save keeps the box open with the text and the reason, and Enter again retries with the same client id,
/// so a lost answer still makes one todo. Esc closes it and drops the text.
/// </summary>
internal sealed class QuickAddViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly Func<Uri?> _url;
    private readonly TimeProvider _clock;
    private string? _clientId;   // one per Enter on this text; a retry sends the same, a changed text gets a new one
    private int _opening;        // which opening of the box this is; an answer is for the one that sent it

    internal QuickAddViewModel(ISindriApi api, Func<Uri?> url, TimeProvider clock)
    {
        (_api, _url, _clock) = (api, url, clock);
    }

    /// <summary>Raised when Sindri took a todo from the box (before the box closes).</summary>
    internal event EventHandler<QuickAddedEventArgs>? Added;

    /// <summary>The box's accessible name and its field's.</summary>
    public string Name { get; } = Labels.QuickAddName;

    /// <summary>The field's placeholder.</summary>
    public string Placeholder { get; } = Labels.QuickAddPlaceholder;

    /// <summary>True while the box is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The typed text. Changing it works out the line under the field again and forgets an old failure.</summary>
    public string Text
    {
        get;
        set
        {
            Set(ref field, value ?? string.Empty);
            _clientId = null;
            Error = string.Empty;
            Preview = PreviewOf(Text);
        }
    } = string.Empty;

    /// <summary>The line under the field: "Due Fri 11-13", "No date" or "Type a todo first". A polite live region.</summary>
    public string Preview { get; private set => Set(ref field, value); } = Labels.QuickAddNoDate;

    /// <summary>Why the last save failed; empty otherwise.</summary>
    public string Error
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasError));
        }
    } = string.Empty;

    /// <summary>True while <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>True while a save waits for its answer.</summary>
    public bool IsSaving { get; private set => Set(ref field, value); }

    /// <summary>Opens the box with an empty field. An open box stays as it is.</summary>
    internal void Open()
    {
        if (IsOpen)
        {
            return;
        }

        _opening++;
        Text = string.Empty;
        IsOpen = true;
    }

    /// <summary>Closes the box and drops the text (Esc, or a save that worked).</summary>
    internal void Close()
    {
        _opening++;
        IsOpen = false;
        IsSaving = false;   // an old answer is dropped by the opening counter, so it must not hold Enter
        Text = string.Empty;
    }

    /// <summary>Enter: saves the todo, or says why not. The box closes only when Sindri took it.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (!IsOpen || IsSaving)
        {
            return;
        }

        var read = TodoQuickParse.Parse(Text, Today());
        if (read.Text.Length == 0)
        {
            Preview = Labels.QuickAddEmpty;
            return;
        }

        if (_url() is not { } url)
        {
            Error = Labels.QuickAddNotAnswering;
            return;
        }

        var opening = _opening;
        _clientId ??= "ui-" + Guid.NewGuid().ToString("N");
        var add = new TodoAdd(read.Text, read.Due is { } due ? due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty, Links: null, _clientId);
        (Error, IsSaving) = (string.Empty, true);
        try
        {
            var saved = await _api.AddTodo(url, add, cancellationToken).ConfigureAwait(true);
            if (opening == _opening)   // closed while the answer was on its way: it belongs to nothing on screen
            {
                Finish(saved, read.Due);
            }
        }
        finally
        {
            if (opening == _opening)
            {
                IsSaving = false;
            }
        }
    }

    private void Finish(Result<TodoAcceptedView> saved, DateOnly? due)
    {
        if (saved.IsError)
        {
            Error = saved.Error.Code.StartsWith("SND-UI-", StringComparison.Ordinal) ? Labels.QuickAddNotAnswering : saved.Error.Message;
            return;
        }

        Added?.Invoke(this, new QuickAddedEventArgs(saved.Value, due));
        Close();
    }

    private DateOnly Today() => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    private string PreviewOf(string text) =>
        TodoQuickParse.Parse(text, Today()).Due is { } due ? Labels.QuickAddDue(due) : Labels.QuickAddNoDate;
}
