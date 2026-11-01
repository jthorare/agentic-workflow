using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The "Add several pages" sheet (pages spec §3.6): paste a list, read it as one row per page (QUERY /sindri/context/pages/preview), tick the rows, and write every ticked row in
/// one profile write with the preview's hash. A row the server gave a reason for stays unticked.
/// </summary>
internal sealed partial class AddSeveralPagesViewModel : ObservableObject
{
    private readonly IPageWrites _writes;
    private readonly string? _repo;
    private readonly IReadOnlyList<ConceptChoice> _known;
    private ProfileWaiting? _waiting;
    private string? _hash;
    private string _initialTicks = string.Empty;
    private string _readText = string.Empty;
    private string? _acceptConcept;
    private IReadOnlyList<string>? _acceptKeys;   // null in paste mode
    private bool _pickConcepts;

    /// <summary>Builds a sheet that has read nothing yet.</summary>
    /// <param name="writes">Where the list is read and the pages are written.</param>
    /// <param name="repo">The repo to read the list for, or null.</param>
    /// <param name="known">The concepts a row can pick for a concept name that matched none, in the Context page's order.</param>
    internal AddSeveralPagesViewModel(IPageWrites writes, string? repo, IReadOnlyList<ConceptChoice>? known = null)
    {
        ArgumentNullException.ThrowIfNull(writes);
        (_writes, _repo, _known) = (writes, repo, known ?? []);
    }

    /// <summary>Builds the Accept all suggested sheet: no paste box, the section's suggested pages as ticked rows once <see cref="LoadSuggested"/> has read them.</summary>
    /// <param name="writes">Where the pages are read and written.</param>
    /// <param name="concept">The concept the section is open on, or null.</param>
    /// <param name="keys">The section's page keys, in its order.</param>
    /// <param name="known">The concepts a row can pick.</param>
    /// <returns>The sheet.</returns>
    internal static AddSeveralPagesViewModel AcceptAll(IPageWrites writes, string? concept, IReadOnlyList<string> keys, IReadOnlyList<ConceptChoice>? known = null)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return new AddSeveralPagesViewModel(writes, repo: null, known) { Title = Labels.AcceptAllSuggested, _acceptConcept = concept, _acceptKeys = [.. keys] };
    }

    /// <summary>Builds the Accept all suggested sheet of the pages in no concept: each row gets a concept picker, and the pick is written onto every action of the page.</summary>
    /// <param name="writes">Where the pages are read and written.</param>
    /// <param name="keys">The section's page keys, in its order.</param>
    /// <param name="known">The concepts a row can pick.</param>
    /// <returns>The sheet.</returns>
    internal static AddSeveralPagesViewModel AcceptAllNoConcept(IPageWrites writes, IReadOnlyList<string> keys, IReadOnlyList<ConceptChoice> known)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return new AddSeveralPagesViewModel(writes, repo: null, known) { Title = Labels.AcceptAllSuggested, _acceptKeys = [.. keys], _pickConcepts = true };
    }

    /// <summary>Raised once the pages are written: the section reads again.</summary>
    internal event EventHandler? Saved;

    /// <summary>Raised when the sheet is done and goes away, saved or not.</summary>
    internal event EventHandler? Closed;

    /// <summary>Raised when focus should move.</summary>
    internal event EventHandler<ValueEventArgs<AddSeveralPagesFocus>>? FocusRequested;

    /// <summary>Gets the sheet's title.</summary>
    public string Title { get; private init; } = Labels.AddSeveralPages;

    /// <summary>Gets a value indicating whether the paste box shows: not in Accept all suggested.</summary>
    public bool HasPasteBox => _acceptKeys is null;

    /// <summary>Gets "186 more: accept these first" when the sheet lists fewer suggested pages than the section has, else empty.</summary>
    public string MoreText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the more line shows.</summary>
    public bool HasMore => MoreText.Length > 0;

    /// <summary>Gets the paste box's label.</summary>
    public string BoxLabel { get; } = Labels.AddSeveralPagesBoxLabel;

    /// <summary>Gets the paste box's description.</summary>
    public string BoxHelp { get; } = Labels.AddSeveralPagesBoxHelp;

    /// <summary>Gets the Read the list button's words.</summary>
    public string ReadText { get; } = Labels.AddSeveralPagesRead;

    /// <summary>Gets the close affordance's words.</summary>
    public string EscText { get; } = Labels.AddSeveralPagesEsc;

    /// <summary>Gets the close affordance's name for a screen reader.</summary>
    public string CloseName { get; } = Labels.AddSeveralPagesCloseName;

    /// <summary>Gets or sets the pasted list.</summary>
    public string Text
    {
        get;
        set
        {
            field = value;
            ChangedSave();   // rows read from other text are stale until Read the list runs again
        }
    } = string.Empty;

    /// <summary>Gets the line under the box: nothing pasted, a refusal of the paste, or a read that failed.</summary>
    public string TextNote { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the line under the box shows.</summary>
    public bool HasTextNote => TextNote.Length > 0;

    /// <summary>Gets one row per block of the last read list.</summary>
    public IReadOnlyList<AddSeveralPagesRowViewModel> Rows { get; private set; } = [];

    /// <summary>Gets a value indicating whether a list was read, so the rows and the save buttons show.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Gets the index in <see cref="Rows"/> of the first row that has a reason, or -1.</summary>
    public int ProblemRow { get; private set; } = -1;

    /// <summary>Gets a value indicating whether a read is in flight.</summary>
    public bool IsReading { get; private set; }

    /// <summary>Gets a value indicating whether a write is in flight.</summary>
    public bool IsSaving { get; private set; }

    /// <summary>Gets a value indicating whether Save all can be pressed: the rows were read from this text, some row is ticked and nothing is in flight.</summary>
    public bool CanSave => !IsSaving && (_acceptKeys is null || _hash is not null) && string.Equals(Text, _readText, StringComparison.Ordinal) && Rows.Any(static r => r.Ticked);

    /// <summary>Gets the save button's words: "Saving…", "Save all and approve" when only these pages wait, else "Save all".</summary>
    public string SaveText => IsSaving ? Labels.PageSheetSaving : Approves(Pending()) ? Labels.AddSeveralPagesSaveAllAndApprove : Labels.AddSeveralPagesSaveAll;

    /// <summary>Gets the line under the buttons: the clash or a failure.</summary>
    public string Note { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the note shows.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Gets a value indicating whether the discard question shows.</summary>
    public bool IsConfirmingDiscard { get; private set; }

    /// <summary>Gets the discard question.</summary>
    public string DiscardQuestion { get; } = Labels.PageSheetDiscardQuestion;

    /// <summary>Gets the discard button's words.</summary>
    public string DiscardText { get; } = Labels.PageSheetDiscard;

    /// <summary>Gets the keep editing button's words.</summary>
    public string KeepEditingText { get; } = Labels.PageSheetKeepEditing;

    /// <summary>Gets a value indicating whether anything was pasted or ticked differently from what the read list offered.</summary>
    internal bool HasChanges => Text.Trim().Length > 0 || !string.Equals(Ticks(), _initialTicks, StringComparison.Ordinal);

    /// <summary>Reads the pasted list: blank asks nothing; otherwise one row per block, and focus goes to the first row with a reason, else to Save all.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the rows are drawn.</returns>
    internal async Task ReadList(CancellationToken cancellationToken)
    {
        if (IsReading)
        {
            return;
        }

        if (Text.Trim().Length == 0)
        {
            SayText(Labels.AddSeveralPagesNothingPasted);
            return;
        }

        var text = Text;   // what is sent is what the rows are read from, whatever is typed while the read runs
        SayText(string.Empty);
        Say(string.Empty);
        IsReading = true;
        OnChanged(nameof(IsReading));
        var read = await Read(text, cancellationToken).ConfigureAwait(true);
        if (read is null)
        {
            return;   // the sheet closed while the list was read
        }

        if (read.Preview is not { } preview)
        {
            SayText(Refusal(read));
            return;
        }

        Fill(preview, text);
        FocusRequested?.Invoke(this, new ValueEventArgs<AddSeveralPagesFocus>(ProblemRow >= 0 ? AddSeveralPagesFocus.ProblemRow : AddSeveralPagesFocus.SaveAll));
    }

    private async Task<PagesPreviewRead?> Read(string text, CancellationToken cancellationToken)
    {
        try
        {
            var read = await _writes.Preview(text, _repo, cancellationToken).ConfigureAwait(true);
            _waiting = read.Preview is null ? null : await _writes.Waiting(cancellationToken).ConfigureAwait(true);
            return read;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            IsReading = false;
            OnChanged(nameof(IsReading));
        }
    }

    private static string Refusal(PagesPreviewRead read) =>
        read is { Code: { } code, Message: { Length: > 0 } message } && code.StartsWith("SND-CONTEXT-", StringComparison.Ordinal) ? message : Labels.AddSeveralPagesUnreadable;

    private void Fill(Api.Context.PagesPreview preview, string text)
    {
        (_hash, _readText) = (preview.ProfileHash, text);
        SetRows([.. preview.Blocks.Select(b => new AddSeveralPagesRowViewModel(b, _known))]);
    }

    private void SetRows(IReadOnlyList<AddSeveralPagesRowViewModel> rows)
    {
        Rows = rows;
        foreach (var row in Rows)
        {
            row.PropertyChanged += (_, _) => ChangedSave();
        }

        ProblemRow = Rows.ToList().FindIndex(static r => r.HasProblem);
        _initialTicks = Ticks();
        OnChanged(nameof(Rows));
        OnChanged(nameof(HasRows));
        OnChanged(nameof(ProblemRow));
        ChangedSave();
    }

    /// <summary>Closes the sheet and drops the changes.</summary>
    internal void Close() => Closed?.Invoke(this, EventArgs.Empty);

    private string Ticks() => string.Concat(Rows.Select(static r => (r.Ticked ? '1' : '0') + r.Signature));

    private void SayText(string note)
    {
        TextNote = note;
        OnChanged(nameof(TextNote));
        OnChanged(nameof(HasTextNote));
    }

    private void Say(string note)
    {
        Note = note;
        OnChanged(nameof(Note));
        OnChanged(nameof(HasNote));
    }

    private void ChangedSave()
    {
        OnChanged(nameof(CanSave));
        OnChanged(nameof(SaveText));
    }
}
