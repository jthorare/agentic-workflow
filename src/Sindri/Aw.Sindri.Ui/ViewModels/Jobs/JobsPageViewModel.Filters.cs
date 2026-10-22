using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>The Jobs page's filters (continued): the served state and kind query parameters, and the line above a table with nothing running.</summary>
internal sealed partial class JobsPageViewModel
{
    private readonly Dictionary<string, string> _kinds = new(StringComparer.Ordinal);

    /// <summary>The column heads.</summary>
    public IReadOnlyList<string> Columns { get; } = Labels.JobColumns;

    /// <summary>The status choices; the first is no filter.</summary>
    public IReadOnlyList<string> StateChoices { get; } = Labels.JobStateChoices;

    /// <summary>The kind choices seen so far; the first is no filter.</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> KindChoices { get; } = [Labels.AllKinds];

    /// <summary>The status filter as shown; choosing one reads the list again.</summary>
    public string SelectedState
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                Set(ref field, value);
                Reread();
            }
        }
    } = Labels.JobStateChoices[0];

    /// <summary>The kind filter as shown; choosing one reads the list again.</summary>
    public string SelectedKind
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                Set(ref field, value);
                Reread();
            }
        }
    } = Labels.AllKinds;

    /// <summary>True when the table has jobs and none of them runs.</summary>
    public bool IsNoneRunning { get; private set => Set(ref field, value); }

    /// <summary>The line above that table.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string NoneRunningText => Labels.NoRunningJobs;
#pragma warning restore CA1822, MA0041

    private JobQuery Query() => new(
        Labels.JobStateServed(SelectedState),
        _kinds.TryGetValue(SelectedKind, out var kind) ? kind : null);

    // A filter changed: read again now, as the catch-up read (observed through CatchUp), when the page is shown.
    private void Reread()
    {
        if (IsActive && _url is { } url)
        {
            Dispose();
            _shown = new CancellationTokenSource();
            CatchUp = CatchUpRead(url, _shown.Token);
        }
    }

    // The kinds a read served become the kind filter's choices, so the choice list is what Sindri has run.
    private void NoteKinds(IEnumerable<Api.Jobs.JobRowView> jobs)
    {
        foreach (var kind in jobs.Select(j => j.Kind).Distinct(StringComparer.Ordinal))
        {
            var word = Labels.JobKind(kind);
            if (_kinds.TryAdd(word, kind))
            {
                KindChoices.Add(word);
            }
        }
    }
}
