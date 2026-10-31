using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The Add several sheet's write (concept pins spec §3.6 "The write", "Sheet states"): every ticked row in ONE PinConcepts job, then the shared approve flow.</summary>
#pragma warning disable MA0182 // Q7b: the shell sheet mounts it
internal sealed partial class PinSeveralViewModel
{
    /// <summary>Writes every ticked row in one profile write, then approves it when nothing else waited at open; closes the sheet when the write landed.</summary>
    /// <param name="cancellationToken">Cancels the wait for the write's job.</param>
    /// <returns>A task that ends when the write is settled.</returns>
    internal async Task PinAllAsync(CancellationToken cancellationToken)
    {
        if (!CanSubmit)
        {
            return;
        }

        var rows = WrittenRows();
        var button = SubmitText;
        _writing = true;
        SetMessage(string.Empty);
        Changed();
        try
        {
            var body = new PinConceptsBody(Set: [.. rows.Select(Body)], IfHash: _ifHash);
            var kept = rows.Where(r => r.Updates is not null).Select(IdOf).ToList();
            var derived = rows.Where(r => r.Updates is null).Select(IdOf).ToList();
            var written = await _writes.Run((url, token) => _api.PinConcepts(url, body, token), path => PinSeveralIds.Mine(path, kept, derived), _approve, cancellationToken).ConfigureAwait(true);
            await Settle(written, button, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _writing = false;
            Changed();
        }
    }

    // A row that updates a pin sends only what its line gives: a definition and words, never features, so the pin's own are kept.
    private static PinSetBody Body(PinSeveralRowViewModel row)
    {
        var summary = row.Definition.Trim() is { Length: > 0 } definition ? definition : null;
        List<string> words = [.. row.Words.Where(w => w.Ticked).Select(w => w.Word)];
        return row.Updates is { } pin
            ? new PinSetBody(pin.Name, pin.Id, summary, words.Count == 0 ? null : words, Features: null)
            : new PinSetBody(row.Name.Trim(), Id: null, summary, words, [.. row.Features.Where(f => f.Ticked).Select(f => f.Key)]);
    }

    private async Task Settle(ProfileWritten written, string button, CancellationToken cancellationToken)
    {
        if (written.State is ProfileWriteState.Approved or ProfileWriteState.Waiting)
        {
            _close();
            return;
        }

        if (written.State == ProfileWriteState.Clash)
        {
            SetMessage(Labels.PinSheetClash(button));
            await ReadPinsAsync(cancellationToken).ConfigureAwait(true);
            return;
        }

        SetMessage(Sentence(written));
    }

    // After a clash the page's pins are stale: read the pins and the file hash together, match the rows against the new pins and retry with that hash. A read that fails leaves no hash, so nothing is written.
    private async Task ReadPinsAsync(CancellationToken cancellationToken)
    {
        var read = _apiUrl() is { } url ? await _api.ContextPins(url, _open.Repo, cancellationToken).ConfigureAwait(true) : null;
        if (read is not { IsSuccess: true })
        {
            _ifHash = null;
            SetMessage(string.Empty);
            Changed();
            return;
        }

        (_pins, _ifHash) = (read.Value.Pins, read.Value.FileHash);
        foreach (var row in Rows)
        {
            Fill(row);
        }

        Evaluate();
    }

    // A timeout is the sheet's own sentence; a refusal shows the server's.
    private static string Sentence(ProfileWritten written) =>
        string.Equals(written.Code, ProfileWriteFlow.TimedOut, StringComparison.Ordinal) ? Labels.PinSheetTimedOut : written.Message ?? Labels.PinSeveralFailed;
}
#pragma warning restore MA0182
