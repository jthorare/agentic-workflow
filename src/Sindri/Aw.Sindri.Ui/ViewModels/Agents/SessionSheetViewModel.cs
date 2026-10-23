using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The session sheet (collector spec CU-3): one session's fields, host, state history and the pending Focus. Read-only; the Focus send is
/// U11a-f's. It reads the session on open and again on every event while the sheet is open (the shell gates that).
/// </summary>
/// <param name="api">The API.</param>
internal sealed partial class SessionSheetViewModel(ISindriApi api) : ObservableObject
{
    private Uri? _url;
    private int _generation;
    private int _copies;
    private CatalogViewModel? _catalog;
    private TimeProvider _clock = TimeProvider.System;

    /// <summary>The API the sheet reads.</summary>
    internal ISindriApi Api { get; } = api;

    /// <summary>The live preview's seam (CO-7c, CU-8): null until CO-7c types and fills it. The view's LivePreviewHost stays hidden and empty while it is null.</summary>
    public object? Preview { get; private set => Set(ref field, value); }

    /// <summary>The session's ref, provider/id; empty before the first.</summary>
    public string Ref { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsShown));
            OnChanged(nameof(IsLoading));
            OnChanged(nameof(IsProblem));
        }
    }

    /// <summary>True while a session's fields are shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == FramePhase.Loading;

    /// <summary>True while an unreadable or refused answer is the whole sheet.</summary>
    public bool IsProblem => Phase is FramePhase.Unreadable or FramePhase.Refused;

    /// <summary>The words of the first read pending.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string LoadingText => Labels.ReadingSession;

    /// <summary>The history's heading.</summary>
    public string HistoryHeading => Labels.SheetHistory;

    /// <summary>The closed disclosure that holds the technical fields, the history and Watch the agent.</summary>
    public string DetailsLabel => Labels.SheetDetails;

    /// <summary>The Watch the agent button's words.</summary>
    public string FocusLabel => Labels.FocusLabel;
#pragma warning restore CA1822, MA0041

    /// <summary>The provider and the title (or repo and id): "Claude Code private title".</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The state line: chip, "estimated", reason and tool.</summary>
    public string StateLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The session's fields; a value the API doesn't serve is not listed.</summary>
    public IReadOnlyList<SessionFieldViewModel> Fields { get; private set => Set(ref field, value); } = [];

    /// <summary>Where it runs; a value the API doesn't serve is not listed.</summary>
    public IReadOnlyList<SessionFieldViewModel> HostFields { get; private set => Set(ref field, value); } = [];

    /// <summary>The states it was in, oldest first.</summary>
    public IReadOnlyList<StateHistoryEntryViewModel> History
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasHistory));
        }
    } = [];

    /// <summary>True when there is a history to list.</summary>
    public bool HasHistory => History.Count > 0;

    /// <summary>True once the session has ended (the read answered 404).</summary>
    public bool IsEnded
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsFocusEnabled));
        }
    }

    /// <summary>"This session has ended." while it has; empty otherwise.</summary>
    public string EndedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The 404's served detail, for a tooltip only.</summary>
    public string EndedDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>A refusal's or an unreadable answer's sentence; empty otherwise.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The stale note: the last read failed and the fields are the last read's.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Focus control from the catalog; null while the catalog is unread or lists none.</summary>
    public ControlViewModel? Focus { get; private set => Set(ref field, value); }

    /// <summary>The Watch the agent button's accessible name: "Watch the agent, coming soon".</summary>
    public string FocusName { get; private set => Set(ref field, value); } = Labels.FocusLabel;

    /// <summary>The pending Focus line: the phase and the CLI filled with the ref; empty without a Focus control.</summary>
    public string FocusLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The CLI to copy, filled with the ref.</summary>
    public string FocusCli
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasFocusLine));
        }
    } = string.Empty;

    /// <summary>True when the pending Focus line and its Copy are drawn.</summary>
    public bool HasFocusLine => FocusLine.Length > 0;

    /// <summary>True when Focus can run: a live session and a sender (none until U11a-f).</summary>
    public bool IsFocusEnabled => !IsEnded && Focus?.CanRun == true;

    /// <summary>"Copy", or "Copied" for a moment.</summary>
    public string CopyLabel
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CopyName));
        }
    } = Labels.Copy;

    /// <summary>The Copy button's accessible name: "Copy: Watch the agent", then "Copied".</summary>
    public string CopyName => string.Equals(CopyLabel, Labels.Copied, StringComparison.Ordinal) ? Labels.Copied : Labels.Copy + ": " + Labels.FocusLabel;

    /// <summary>Gives the sheet the catalog (Focus's row) and the clock (Copy's "Copied").</summary>
    /// <param name="catalog">The command catalog.</param>
    /// <param name="clock">The clock.</param>
    internal void Wire(CatalogViewModel catalog, TimeProvider clock)
    {
        (_catalog, _clock) = (catalog, clock);
        catalog.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal))
            {
                DrawFocus();
            }
        };
        DrawFocus();
    }

    /// <summary>Names the session now, before any read (a deep link, even before the app is connected).</summary>
    /// <param name="reference">The session's ref, provider/id.</param>
    internal void Show(string reference)
    {
        if (string.Equals(reference, Ref, StringComparison.Ordinal))
        {
            return;
        }

        _generation++;
        Clear();
        Ref = reference;
        Heading = reference;
        Phase = FramePhase.Loading;
        DrawFocus();
    }

    /// <summary>Reads the session behind a ref and shows it.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="reference">The session's ref, provider/id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Open(Uri url, string reference, CancellationToken cancellationToken)
    {
        Show(reference);
        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Reads the open session again (on every event while the sheet is open).</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Ref.Length == 0 ? Task.CompletedTask : Read(cancellationToken);
    }

    /// <summary>Copies the Focus CLI: "Copied" for <see cref="ControlViewModel.CopiedFor"/>.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends when the clipboard answered.</returns>
    internal async Task CopyFocus(CancellationToken cancellationToken)
    {
        if (_catalog is not { } catalog || FocusCli.Length == 0 || !await catalog.Clipboard.SetText(FocusCli, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyLabel = Labels.Copied;
        _ = Uncopy(++_copies, cancellationToken);
    }

    private async Task Uncopy(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ControlViewModel.CopiedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (copy == _copies)
        {
            CopyLabel = Labels.Copy;
        }
    }

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Ref.Split('/', 2) is not [var provider, var id])
        {
            return;
        }

        var generation = ++_generation;
        var answer = await Api.Session(url, provider, id, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another session, took over
        }

        if (answer.IsSuccess)
        {
            Draw(answer.Value);
        }
        else
        {
            Failed(answer.Error);
        }
    }

    private void Failed(Error error)
    {
        if (error.Kind == ErrorKind.NotFound)
        {
            (IsEnded, EndedText, EndedDetail) = (true, Labels.SessionEnded, error.Message);
            DrawEndedResume();
            Phase = IsShown ? Phase : FramePhase.NotFound;   // what was shown stays, with the sentence above it
        }
        else if (IsShown)
        {
            Note = Labels.SessionStale(error);
        }
        else
        {
            ProblemText = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal) ? Labels.SessionUnreadable : Labels.InstanceRefused(error);
            Phase = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal) ? FramePhase.Unreadable : FramePhase.Refused;
        }
    }

    private void Clear()
    {
        (Heading, StateLine, EndedText, EndedDetail, ProblemText, Note) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (Fields, HostFields, History, IsEnded, _step) = ([], [], [], false, null);
        (Sections, IsAsking, InStatusText, RefCopy) = ([], false, string.Empty, null);
        EndedResumeText = string.Empty;
        OnChanged(nameof(HasStep));
    }

    private static string Id8(string id) => id.Length > 8 ? id[..8] : id;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
