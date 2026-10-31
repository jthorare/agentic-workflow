using System.Collections.Concurrent;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>
/// The Add several sheet (concept pins spec §3.6, mockup several B): a box for the owner's list, <b>Read the list</b> turns it into one row per concept (each matched to a derived concept of the map, with its words and features
/// filled in as on Pin this), and <b>Pin all</b> writes every ticked row in one profile write. It reads and writes through the ports it is handed; the shell mounts it in its sheet.
/// </summary>
#pragma warning disable MA0182 // Q7b: the shell sheet mounts it
internal sealed partial class PinSeveralViewModel : ObservableObject
{
    private const int ConceptReads = 4;

    private readonly PinSheetOpen _open;
    private readonly ISindriApi _api;
    private readonly Func<Uri?> _apiUrl;
    private readonly ProfileWriteFlow _writes;
    private readonly Action _close;
    private readonly Func<CancellationToken, Task<string?>> _pinsHash;
    private readonly List<PinSeveralChoice> _derived;
    private readonly ConcurrentDictionary<string, PinSuggestView?> _concepts = new(StringComparer.Ordinal);
    private string? _ifHash;
    private bool _approve;
    private bool _started;
    private bool _matching;
    private bool _matchFailed;
    private bool _writing;

    /// <summary>Builds the sheet.</summary>
    /// <param name="open">What opened it: the repo and every pin of the live profile.</param>
    /// <param name="concepts">The concept layer's cards; the derived ones are the match picker's choices.</param>
    /// <param name="api">Sindri's API.</param>
    /// <param name="apiUrl">The control plane's API address now, or null when none is known.</param>
    /// <param name="writes">The write-then-approve flow (it holds the clock for the job wait).</param>
    /// <param name="close">Closes the sheet (a write that landed, approved or waiting).</param>
    /// <param name="pinsHash">Reads the hash of the profile file the pins came from, the write's ifHash; null reads it through <paramref name="api"/>'s pins read.</param>
    internal PinSeveralViewModel(
        PinSheetOpen open, IReadOnlyList<ConceptTileViewModel> concepts, ISindriApi api, Func<Uri?> apiUrl, ProfileWriteFlow writes, Action close, Func<CancellationToken, Task<string?>>? pinsHash = null)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(concepts);
        (_open, _api, _apiUrl, _writes, _close) = (open, api, apiUrl, writes, close);
        _pinsHash = pinsHash ?? ReadPinsHash;
        _derived = [.. concepts.Where(c => !c.IsSetByPerson && !string.Equals(c.Id, "concept:other", StringComparison.Ordinal)).Select(c => new PinSeveralChoice(c.Id, c.Name))];
    }

    /// <summary>The rows were read: the view moves focus to the first one.</summary>
    internal event EventHandler? RowsRead;

    /// <summary>The sheet's heading.</summary>
    public string Title { get; } = Labels.PinSeveralTitle;

    /// <summary>The box's accessible name.</summary>
    public string BoxName { get; } = Labels.PinSeveralBox;

    /// <summary>The box's placeholder.</summary>
    public string Hint { get; } = Labels.PinSeveralHint;

    /// <summary>The Read the list button's text.</summary>
    public string ReadText { get; } = Labels.PinSeveralRead;

    /// <summary>The name box's accessible name on a row.</summary>
    public string NameBoxName { get; } = Labels.PinSeveralNameBox;

    /// <summary>The definition box's accessible name on a row.</summary>
    public string DefinitionBoxName { get; } = Labels.PinSeveralDefinitionBox;

    /// <summary>The match picker's accessible name on a row.</summary>
    public string PickerName { get; } = Labels.PinSeveralPicker;

    /// <summary>The list as typed or pasted.</summary>
    public string Text
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                field = value ?? string.Empty;
                OnChanged(nameof(Text));
                OnChanged(nameof(CanRead));
            }
        }
    } = string.Empty;

    /// <summary>Gets a value indicating whether Read the list works: the box has words and nothing is being read or written.</summary>
    public bool CanRead => Text.Trim().Length > 0 && !_matching && !_writing;

    /// <summary>One row per concept of the list.</summary>
    public IReadOnlyList<PinSeveralRowViewModel> Rows
    {
        get;
        private set
        {
            field = value;
            OnChanged(nameof(Rows));
        }
    } = [];

    /// <summary>"Up to 100 at a time; the first 100 are read." when the list was longer; empty otherwise.</summary>
    public string TooManyNote { get; private set; } = string.Empty;

    /// <summary>Whether the too-many note has words.</summary>
    public bool HasTooManyNote => TooManyNote.Length > 0;

    /// <summary>The line under the rows after a write that did not land: the clash, the server's sentence or the failure; empty otherwise.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Whether the message has words.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>The submit button: "Pin all and approve", "Pin all" or "Pinning 12…".</summary>
    public string SubmitText => _writing ? Labels.PinSeveralSubmitting(WrittenRows().Count) : Labels.PinSeveralSubmit(_approve);

    /// <summary>Gets a value indicating whether the submit button works: a row is ticked and nothing is being matched or written.</summary>
    public bool CanSubmit => !_writing && !_matching && WrittenRows().Count > 0;

    /// <summary>"Tick at least one line." once the list is read and no row is ticked; empty otherwise.</summary>
    public string SubmitNote => Rows.Count > 0 && !_matching && !_writing && WrittenRows().Count == 0 ? Labels.PinSeveralTickOne : string.Empty;

    /// <summary>Whether the submit note has words.</summary>
    public bool HasSubmitNote => SubmitNote.Length > 0;

    /// <summary>Reads the pins hash and what waits for approval, once, when the sheet opens: the write's ifHash and whether its button approves.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that ends when both are read.</returns>
    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await ReadStateAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task ReadStateAsync(CancellationToken cancellationToken)
    {
        _ifHash = await _pinsHash(cancellationToken).ConfigureAwait(true);
        var waiting = await _writes.Waiting(cancellationToken).ConfigureAwait(true);
        _approve = waiting is { ChangedPaths.Count: 0 };
        Changed();
    }

    private async Task<string?> ReadPinsHash(CancellationToken cancellationToken)
    {
        if (_apiUrl() is not { } url)
        {
            return null;
        }

        var read = await _api.ContextPins(url, _open.Repo, cancellationToken).ConfigureAwait(true);
        return read.IsSuccess ? read.Value.FileHash : null;
    }

    private IReadOnlyList<PinSeveralRowViewModel> WrittenRows() => [.. Rows.Where(r => r.IsWritten).DistinctBy(IdOf, StringComparer.Ordinal)];

    private static string IdOf(PinSeveralRowViewModel row) => row.Updates?.Id ?? PinSeveralIds.Slug(row.Name);

    // The submit button and the read button follow the rows and the phase.
    private void Changed()
    {
        OnChanged(nameof(CanRead));
        OnChanged(nameof(SubmitText));
        OnChanged(nameof(CanSubmit));
        OnChanged(nameof(SubmitNote));
        OnChanged(nameof(HasSubmitNote));
    }

    private void SetMessage(string message)
    {
        Message = message;
        OnChanged(nameof(Message));
        OnChanged(nameof(HasMessage));
    }
}
#pragma warning restore MA0182
