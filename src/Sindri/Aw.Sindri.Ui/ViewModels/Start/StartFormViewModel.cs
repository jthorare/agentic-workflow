using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>
/// The Start form (screens spec §4.2, U5'): choose an approved recipe and an item, give the recipe's inputs, and send
/// StartInstance once. The subject is the item. The recipe list only picks; the inputs come from the recipe's own read.
/// A refusal is the API's code and detail, shown inline; nothing is retried by the app, and a start is marked sent only
/// when the API accepted it. Readiness is the command catalog's StartInstance row.
/// </summary>
internal sealed partial class StartFormViewModel : ObservableObject
{
    private const string Action = "StartInstance";
    private const string ItemFactName = "item.json";

    private readonly CatalogViewModel _catalog;
    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly IFilePicker _files;
    private int _form;   // which opening of the form this is; an answer is for the one that sent it
    private RecipeChoice? _chosen;   // the recipe whose inputs were asked for
    private string? _itemKey;   // the recipe's own key for its item input, when it declares one
    private bool _blocked;   // the recipes or the recipe could not be read, or there is none to start
    private bool _sentFirst;
    private bool _sentNext;
    private bool _reading;   // the chosen recipe's inputs are being read: Start waits for them
    private int _choice;   // counts the recipe choices: a read answers only the latest one
    private int _edits;   // counts the item and recipe changes: an answer to a send before the latest one is not about what the form shows now

    /// <summary>The form over its ports.</summary>
    /// <param name="catalog">The command catalog: its StartInstance row says whether Start is ready.</param>
    /// <param name="url">The API now, or null when none is running.</param>
    /// <param name="api">The API.</param>
    /// <param name="files">Where a file input is chosen.</param>
    internal StartFormViewModel(CatalogViewModel catalog, Func<Uri?> url, ISindriApi api, IFilePicker files)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(files);
        (_catalog, _url, _api, _files) = (catalog, url, api, files);
        catalog.PropertyChanged += (_, e) =>
        {
            if (IsOpen && string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal))
            {
                RefreshReadiness();   // the command list arrived (or changed) while the form is open
            }
        };
    }

    /// <summary>Raised for every accepted start, so the shell opens the instance's frame.</summary>
    internal event EventHandler<StartedEventArgs>? Started;

    /// <summary>The app's copy for the form.</summary>
    public StartText Text { get; } = StartText.Instance;

    /// <summary>True while the form is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The approved recipes, one for each id.</summary>
    public IReadOnlyList<RecipeChoice> Recipes { get; private set => Set(ref field, value); } = [];

    /// <summary>The recipe chosen (the view calls <see cref="Choose"/> when it changes, which reads its inputs).</summary>
    public RecipeChoice? Recipe { get; set => Set(ref field, value); }

    /// <summary>The chosen recipe's inputs, as fields; an item input is the item field, not one of these.</summary>
    public IReadOnlyList<StartFieldViewModel> Fields { get; private set => Set(ref field, value); } = [];

    /// <summary>The items the queue shows, to fill the item field from.</summary>
    public IReadOnlyList<ItemChoice> Items { get; private set => Set(ref field, value); } = [];

    /// <summary>An item picked from <see cref="Items"/>; picking one fills <see cref="ItemId"/>.</summary>
    public ItemChoice? PickedItem
    {
        get;
        set
        {
            Set(ref field, value);
            if (value is not null)
            {
                ItemId = value.Id;
            }
        }
    }

    /// <summary>The item the workflow runs for.</summary>
    public string ItemId
    {
        get;
        set
        {
            var changed = !string.Equals(field, value, StringComparison.Ordinal);
            Set(ref field, value);
            ClearError();
            if (changed)
            {
                ResetAttempt();   // what an earlier answer said was about the item before this one
                if (PickedItem is { } picked && !string.Equals(picked.Id, value, StringComparison.Ordinal))
                {
                    PickedItem = null;   // the list must not keep showing an item the box no longer names
                }
            }
        }
    } = string.Empty;

    /// <summary>True while the recipes are being read.</summary>
    public bool IsLoading
    {
        get;
        private set
        {
            Set(ref field, value);
            StateChanged();
        }
    }

    /// <summary>The recipes' or the recipe's read failure as served (code and detail), the empty-list sentence, or empty.</summary>
    public string Note
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNote));
            OnChanged(nameof(StartHelp));
            Say(value);
        }
    } = string.Empty;

    /// <summary>True while <see cref="Note"/> shows.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>True when the command catalog lists Start (no row, because the list is not read, is not).</summary>
    public bool IsReady => Row is not null;

    /// <summary>For a Start that is not ready, why: that the command list is not read; else empty.</summary>
    public string Readiness => Row is null ? Labels.StartNoCatalog : string.Empty;

    /// <summary>True when <see cref="Readiness"/> shows.</summary>
    public bool IsNotReady => !IsReady;

    /// <summary>True while a start is in flight.</summary>
    public bool IsSending
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanStart));
            OnChanged(nameof(ButtonText));
            OnChanged(nameof(StartHelp));
            Say(value ? Labels.StartSending : string.Empty);
        }
    }

    /// <summary>The Start button's words: the catalog's label, and "Starting…" while it is in flight.</summary>
    public string ButtonText => IsSending ? Labels.StartSending : Labels.StartWorkflow;

    /// <summary>True when a press would send: ready, nothing blocks it, the recipe's inputs are read, not in flight and not already accepted.</summary>
    public bool CanStart => IsReady && !_blocked && !_reading && !IsSending && !_sentFirst;

    /// <summary>The inline error: the API's code and detail as served, or the sentence of a field that cannot be sent; empty when none.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's SND code, or empty for a field's sentence.</summary>
    public string ErrorCode { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's detail as served, or the field's sentence.</summary>
    public string ErrorDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Which control the error is about; focus goes there.</summary>
    public StartErrorTarget ErrorTarget { get; private set => Set(ref field, value); }

    /// <summary>The input the error is about, for <see cref="StartErrorTarget.Field"/>.</summary>
    public StartFieldViewModel? ErrorField { get; private set => Set(ref field, value); }

    /// <summary>True when the error line shows: an error that has no control of its own to sit under (an input's own sentence is under it).</summary>
    public bool ShowsSummary => HasError && ErrorTarget != StartErrorTarget.Field;

    /// <summary>The recipe picker's error, for its help text; empty when the error is about something else.</summary>
    public string RecipeError => ErrorTarget == StartErrorTarget.Recipe ? Error : string.Empty;

    /// <summary>The item box's error, for its help text; empty when the error is about something else.</summary>
    public string ItemError => ErrorTarget == StartErrorTarget.Item ? Error : string.Empty;

    /// <summary>Counts the errors that ask the view to move focus to the inline error.</summary>
    public int ErrorFocusRequests { get; private set => Set(ref field, value); }

    /// <summary>Counts the existing-attempt answers, each of which asks the view to put focus on the outcome (the pressed Start button is off).</summary>
    public int OutcomeFocusRequests { get; private set => Set(ref field, value); }

    /// <summary>"already running" or "finished" for an attempt that existed; else empty.</summary>
    public string Outcome
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasOutcome));
            OnChanged(nameof(StartHelp));
            Say(value);
        }
    } = string.Empty;

    /// <summary>The words of the polite live region (<see cref="Say"/>): the latest outcome, refusal, read failure, readiness or progress; the resting text while it is dark.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.StartWorkflow;

    /// <summary>True while the live region is live; it goes dark first so that the same words can be said again (docs "LiveSetting on macOS").</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>True while the recipes or the chosen recipe's inputs are being read.</summary>
    public bool IsBusy => IsLoading || _reading;

    /// <summary>What is being read, for the line shown while it is.</summary>
    public string BusyText => _reading ? Labels.StartReadingInputs : Labels.StartLoading;

    /// <summary>Why Start is off, in words (its help text); empty while it can send.</summary>
    public string StartHelp
    {
        get
        {
            if (CanStart)
            {
                return string.Empty;
            }

            return !IsReady ? Readiness
                : _blocked ? Note
                : _reading ? Labels.StartWhyReading
                : IsSending ? Labels.StartWhySending
                : Labels.StartWhySent;
        }
    }

    /// <summary>True while <see cref="Outcome"/> shows.</summary>
    public bool HasOutcome => Outcome.Length > 0;

    /// <summary>True when the existing attempt has finished: a new attempt can be asked for.</summary>
    public bool OffersNewAttempt
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanStartNewAttempt));
        }
    }

    /// <summary>True when the new-attempt button would send: offered, not in flight and not sent yet.</summary>
    public bool CanStartNewAttempt => OffersNewAttempt && !IsSending && !_sentNext;

    /// <summary>True once an accepted start has opened an instance's frame (a new one, or the one an existing attempt is): on close the window moves focus there, not back to the opener.</summary>
    public bool OpenedFrame { get; private set => Set(ref field, value); }

    private CommandRowView? Row => _catalog.Row(Action);
}
