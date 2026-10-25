using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The decision sheet (U3' T6): the question, one button per option as served, the note, and what Decide answered. A decide is
/// sent once and never retried; a refusal (decided elsewhere, voided, not found) shows as served, inline. When the row clears
/// while the sheet is open the form is disabled and keeps its text.
/// </summary>
internal sealed partial class DecisionSheetViewModel(ISindriApi api, Func<Uri?> apiUrl, TimeProvider clock, DecisionSender sender, Platform.IClipboard? clipboard = null) : ObservableObject
{
    private static readonly TimeSpan _outcomeShown = TimeSpan.FromSeconds(2);
    private NeedsYouRowViewModel? _row;

    /// <summary>Raised when Decide was refused, so the shell can open the sheet where the refusal shows.</summary>
    internal event EventHandler? Refused;

    /// <summary>The option buttons, as served (the first is the recommended one). One list, replaced only when an option differs, so the buttons (and the focus on one) survive a re-read and a press; a press in flight is guarded by the sender, not by disabling the button.</summary>
    internal IReadOnlyList<DecisionOption> Options { get; private set; } = [];

    /// <summary>The decision's question as the pending list serves it; the row's title while the list has none.</summary>
    internal string Question => _row is null ? "" : _row.Entry.Question.Length > 0 ? _row.Entry.Question : _row.Title;

    /// <summary>The sheet's title: the request's own words.</summary>
    internal string Heading => _row is null ? "" : _row.Title.Length > 0 ? _row.Title : DecisionId;

    /// <summary>Who filed the request, how long ago and for which item.</summary>
    internal string FiledLine => _row is null ? "" : Labels.DecisionFiled(_row.Entry.FiledBy, _row.Entry.Since == default ? null : clock.GetUtcNow() - _row.Entry.Since, _row.Entry.ItemId);

    /// <summary>What was decided, once Decide was accepted; it stays (unlike <see cref="Outcome"/>).</summary>
    internal string DecidedLine { get; private set => Set(ref field, value); } = "";

    /// <summary>True while the sheet still asks its question: not decided here, not cleared.</summary>
    internal bool IsAsking => !IsCleared && DecidedLine.Length == 0;

    /// <summary>The recorded-only sentence as served; empty for none.</summary>
    internal string Notice => _row?.Notice ?? "";

    /// <summary>The phase and command line under the options when Decide is not ready; empty when it is.</summary>
    internal string DecideLine => _row is null ? "" : DecisionOptions.Line(_row.Controls);

    /// <summary>The served command the line's Copy puts on the clipboard; empty for none.</summary>
    internal string DecideCli => _row is null || DecideLine.Length == 0 ? "" : DecisionOptions.Cli(_row.Controls);

    /// <summary>The Copy button's word.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    internal string CopyLabel => Labels.Copy;
#pragma warning restore CA1822, MA0041

    /// <summary>True when the line has a command to copy.</summary>
    internal bool CanCopy => DecideCli.Length > 0;

    /// <summary>Copies the decision's served Decide command.</summary>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal async Task CopyDecide()
    {
        if (DecideCli.Length > 0 && clipboard is not null)
        {
            _ = await clipboard.SetText(DecideCli, CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>What Copy is called to a screen reader: "Copy: Decide DEC-1".</summary>
    internal string CopyName => Labels.CopyDecide(DecisionId);

    /// <summary>The line's accessible name: it carries the decision it speaks of.</summary>
    internal string DecideLineName => Labels.DecideLineName(DecisionId, DecideLine);

    /// <summary>The id of the decision shown.</summary>
    internal string DecisionId { get; private set; } = "";

    /// <summary>True while a Decide is in flight.</summary>
    internal bool IsSending => sender.IsInFlight(DecisionId);

    /// <summary>The last send; completes when Decide was answered.</summary>
    internal Task Settled { get; private set; } = Task.CompletedTask;

    /// <summary>Completes when the accepted outcome has been shown for 2 s and dropped.</summary>
    internal Task Expiry { get; private set; } = Task.CompletedTask;

    private string ServedCleared { get; set; } = "";

    private bool CanSend => !IsCleared && sender.CanSend(DecisionId);

    /// <summary>Shows a row's decision; another decision starts a fresh form, the same one is updated in place.</summary>
    /// <param name="row">The decision row.</param>
    internal void Show(NeedsYouRowViewModel row)
    {
        var id = row.Entry.Subject?.Id ?? row.Id;
        if (!string.Equals(id, DecisionId, StringComparison.Ordinal) || IsCleared)
        {
            (Note, Refusal, RefusalCode, Outcome, DecidedLine, IsCleared, ServedCleared, IsRefusedAsDecided) = ("", "", "", "", "", false, "", false);
            DecisionId = id;
            OnChanged(nameof(ClearedLine));
        }

        _row = row;
        Raise();
    }

    /// <summary>The row left the list: the form is disabled and keeps its text; the cleared line is the one the decided list serves.</summary>
    /// <returns>A task that completes when the cleared line was read.</returns>
    internal Task Clear()
    {
        if (_row is null || IsCleared)
        {
            return Task.CompletedTask;
        }

        IsCleared = true;
        OnChanged(nameof(ClearedLine));
        Raise();
        Settled = ReadClearedLine(DecisionId);
        return Settled;
    }

    // The decision is found by id in GET /decisions?pending=false (no route by id); not held there, the line stays "<id> cleared".
    private async Task ReadClearedLine(string id)
    {
        var row = await ReadDecided(id).ConfigureAwait(true);
        var line = row is null ? "" : row.DecidedBy is { Length: > 0 } ? Labels.Decided(WordsOf(row)) : row.Cleared ?? "";
        if (line.Length > 0 && string.Equals(DecisionId, id, StringComparison.Ordinal))
        {
            ServedCleared = line;
            OnChanged(nameof(ClearedLine));
        }
    }

    private async Task<DecisionView?> ReadDecided(string id)
    {
        if (apiUrl() is not { } url)
        {
            return null;
        }

        var decided = await api.Decisions(url, pending: false, item: null, CancellationToken.None).ConfigureAwait(true);
        return decided.IsSuccess ? decided.Value.Decisions.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal)) : null;
    }

    private string WordsOf(DecisionView row) => DecisionRefusals.Words(row, clock);

    /// <summary>True when this row is the decision shown.</summary>
    /// <param name="rowId">The row's id.</param>
    /// <returns>True when it is.</returns>
    internal bool Shows(string rowId) => _row is { } row && string.Equals(row.Id, rowId, StringComparison.Ordinal);

    /// <summary>Sends Decide with an option and the note; once per decision, never retried. Does nothing while sending or after the decision was accepted.</summary>
    /// <param name="optionId">The option.</param>
    /// <returns>A task that completes when Decide was answered.</returns>
    internal Task Choose(string optionId)
    {
        if (!CanSend || _row is null || apiUrl() is not { } url)
        {
            return Task.CompletedTask;
        }

        // The decision and its words are taken now: the sheet may show another decision before the answer comes.
        var (id, label, note) = (DecisionId, Options.FirstOrDefault(o => string.Equals(o.Id, optionId, StringComparison.Ordinal))?.Label ?? optionId, Note);
        Settled = Send(url, id, optionId, label, note);
        return Settled;
    }

    private async Task Send(Uri url, string id, string optionId, string label, string note)
    {
        (RefusalCode, Refusal, IsRefusedAsDecided) = ("", "", false);
        var sending = sender.Send(url, id, optionId, note);
        Raise();
        OnChanged(nameof(IsSending));
        var answer = await sending.ConfigureAwait(true);
        OnChanged(nameof(IsSending));
        if (answer is null)
        {
            return;
        }

        var shown = string.Equals(DecisionId, id, StringComparison.Ordinal);
        if (answer.IsError)
        {
            if (shown)
            {
                // Only "decided elsewhere" is put in words; a voided decision or a closed item keeps the sentence the server served.
                var conflict = DecisionRefusals.IsDecidedElsewhere(answer.Error.Code, answer.Error.Message);
                (RefusalCode, Refusal, IsRefusedAsDecided) = (answer.Error.Code, conflict ? Labels.AlreadyDecidedBare : answer.Error.Message, conflict);
                Say(Refusal);
                Raise();
                Refused?.Invoke(this, EventArgs.Empty);
                if (conflict)
                {
                    await ExplainConflict(id).ConfigureAwait(true);
                }
            }

            return;
        }

        if (shown)
        {
            Outcome = Labels.Decided(label);
            DecidedLine = Outcome;
            Say(Outcome);
        }

        Raise();
        Expiry = DropOutcome(id);
    }

    // The refusal says how the decision was answered, in words, once the decided list has it; a voided decision says its served line.
    private async Task ExplainConflict(string id)
    {
        var row = await ReadDecided(id).ConfigureAwait(true);
        var line = row is null ? "" : row.DecidedBy is { Length: > 0 } ? Labels.AlreadyDecided(WordsOf(row)) : row.Cleared ?? "";
        if (line.Length > 0 && string.Equals(DecisionId, id, StringComparison.Ordinal))
        {
            Refusal = line;
            Say(line);
            Raise();
        }
    }

    // The outcome shows for 2 s; the row clears on the refresh the accepted command brings.
    private async Task DropOutcome(string id)
    {
        await Task.Delay(_outcomeShown, clock).ConfigureAwait(true);
        if (string.Equals(DecisionId, id, StringComparison.Ordinal))
        {
            Outcome = "";
        }
    }

    private void Raise()
    {
        var built = _row is null ? [] : DecisionOptions.From(_row.Controls, DecisionId, IsCleared, sender);
        if (!built.SequenceEqual(Options))
        {
            Options = built;
            OnChanged(nameof(Options));
        }

        OnChanged(nameof(Question));
        OnChanged(nameof(Heading));
        OnChanged(nameof(FiledLine));
        OnChanged(nameof(IsAsking));
        OnChanged(nameof(ClearedLine));
        OnChanged(nameof(Notice));
        OnChanged(nameof(DecideLine));
        OnChanged(nameof(DecideCli));
        OnChanged(nameof(CanCopy));
        OnChanged(nameof(CopyName));
        OnChanged(nameof(DecideLineName));
    }

    // The flag changes before the text, and an announcement already live is dropped first so the next one is spoken again.
    private void Say(string text)
    {
        if (IsAnnouncing)
        {
            IsAnnouncing = false;
        }

        Announcement = Labels.Sindri;
        IsAnnouncing = true;
        Announcement = text;
    }
}
