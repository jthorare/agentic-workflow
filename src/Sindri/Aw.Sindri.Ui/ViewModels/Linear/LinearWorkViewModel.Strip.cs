using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The since-you-looked strip (<see cref="LinearWorkViewModel"/>, continued; Linear hub spec §3, REQ-9): a band above the filter bar. It reads from
/// the marker the preferences hold. Dismiss is the only thing that moves that marker: leaving the view, a reload or a glance never does.
/// </summary>
internal sealed partial class LinearWorkViewModel
{
    private int _sinceReads;
    private string? _marker;   // the stored one: what every read asks "after"
    private LinearSince? _since;
    private Dictionary<string, LinearSinceRowViewModel> _stripShown = new(StringComparer.Ordinal);

    /// <summary>How many changes the strip holds (the tab carries it).</summary>
    public int NewCount => _since?.Count ?? 0;

    /// <summary>True when the strip holds changes (Dismiss shows).</summary>
    public bool HasNew => NewCount > 0;

    /// <summary>True when the strip shows: on the Linear view, with changes or with a failed read.</summary>
    public bool ShowsStrip => IsLinear && (NewCount > 0 || StripFailed);

    /// <summary>The strip's region name: "Since you last looked, 3 items".</summary>
    public string StripName => Labels.LinearStripName(NewCount);

    /// <summary>The changes, oldest first.</summary>
    public ObservableCollection<LinearSinceRowViewModel> StripRows { get; } = [];

    /// <summary>True when the changes could not be read: the strip says so and offers Retry; the list below stays.</summary>
    public bool StripFailed { get; private set; }

    /// <summary>Dismiss: stores the answer's marker, so the next read asks after it. The one thing that moves the marker.</summary>
    /// <returns>A task that completes when the marker is written.</returns>
    internal async Task Dismiss()
    {
        if (_since is not { } since)
        {
            return;
        }

        var marker = _marker = since.Marker;
        _sinceReads++;   // a read still on its way asked after the old marker: its answer would show the dismissed rows again
        Show(since: null);
        await _preferences.Update(p => p with { LinearSince = marker }).ConfigureAwait(true);
    }

    /// <summary>Retry: reads the changes again.</summary>
    /// <returns>A task that completes when the read is done.</returns>
    internal Task RetryStrip() => _url is { } url ? Reading = ReadSince(url, _shown?.Token ?? CancellationToken.None) : Task.CompletedTask;

    private async Task ReadSince(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_sinceReads;
        var answer = await _api.LinearSince(url, _marker, cancellationToken).ConfigureAwait(true);
        if (read != _sinceReads)
        {
            return;
        }

        if (answer.IsError)
        {
            StripFailed = true;   // the changes already shown stay under the line
            Notify();
            return;
        }

        Show(answer.Value);
    }

    private void Show(LinearSince? since)
    {
        _since = since is { Count: > 0 } ? since : null;
        StripFailed = false;
        var kept = new Dictionary<string, LinearSinceRowViewModel>(StringComparer.Ordinal);
        foreach (var item in _since?.Items ?? [])
        {
            var id = $"{item.Kind}|{item.Key}|{item.At:O}";
            kept[id] = _stripShown.TryGetValue(id, out var shown) ? shown : new LinearSinceRowViewModel(item);   // a row keeps its button, and its focus, across reads
        }

        _stripShown = kept;
        Patch.To(StripRows, [.. kept.Values]);
        Notify();
    }

    private void Notify()
    {
        foreach (var name in new[] { nameof(NewCount), nameof(HasNew), nameof(ShowsStrip), nameof(StripName), nameof(StripFailed), nameof(LinearTabText), nameof(LinearTabName) })
        {
            OnChanged(name);
        }
    }
}
