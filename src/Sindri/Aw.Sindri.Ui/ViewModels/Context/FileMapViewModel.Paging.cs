using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The Files view's paging and filtering: the next page read ahead of the scroll, and the debounced filter over what is read.</summary>
internal sealed partial class FileMapViewModel
{
    // The rows over what is read; filtered when a filter is set. A page arriving replaces the list in one assignment.
    private void Rebuild()
    {
        _all = Unfiltered();
        Rows = FilterText.Length == 0 ? _all : _all.Narrow(FilterText);
    }

    private LazyFileRows Unfiltered() => new(_entries, Symbols, positions: null, _next is null ? null : _total, Touched);

    private string NoteText() =>
        _next is not null && FilterText.Length > 0
            ? Labels.ContextFilesFilterPartial(_entries.Length, _total ?? _entries.Length)
            : string.Equals(_symbolsServed?.State, "available", StringComparison.Ordinal) ? string.Empty : Labels.ContextMapNamesDetail;

    // The list asked for a row: near the end of what is read, the next page is read ahead of the scroll.
    private void Touched(int index)
    {
        if (_next is not null && !_fetching && index >= _entries.Length - PrefetchAhead)
        {
            _ = FetchNext();
        }
    }

    // A filter reads the rest of the folder, so its answer covers all of it.
    private void FetchMoreForFilter()
    {
        if (_next is not null && FilterText.Length > 0)
        {
            _ = FetchNext();
        }
    }

    // One page at a time, in order; a page that arrives after the folder changed is dropped.
    private async Task FetchNext()
    {
        if (_next is not { } cursor || _fetching || Ports.ApiUrl() is not { } api || _repo is not { } repo)
        {
            return;
        }

        _fetching = true;
        var generation = _generation;
        var path = _current.Length == 0 ? null : _current;
        var read = await Ports.Api.FileMap(api, new FileMapQuery(repo, path, UnmappedOnly: false, Draft: false, cursor, PageSize), CancellationToken.None).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        _fetching = false;
        if (read.IsError)
        {
            StopPaging(read.Error.Message);
            return;
        }

        await Accept(read.Value).ConfigureAwait(true);
        FetchMoreForFilter();
    }

    private void StopPaging(string problem)
    {
        _next = null;
        Problem = problem;
        Rebuild();
        Changed();
    }

    private async Task Accept(FileMapView page)
    {
        _entries = [.. _entries, .. page.Entries];
        _symbolFiles.AddRange(page.Symbols.Files ?? []);
        _next = page.NextCursor;
        _total = page.Total ?? _total;
        Symbols = new SymbolsColumnViewModel(page.Symbols with { Files = [.. _symbolFiles] });
        Note = NoteText();
        if (FilterText.Length == 0)
        {
            Rebuild();
        }
        else
        {
            _all = Unfiltered();
            await ApplyFilter(_filterVersion).ConfigureAwait(true);
        }

        Changed();
    }

    // Typing restarts a pause; only the last text after the pause filters, off the UI thread, and the rows are replaced in one assignment.
    private async Task Refilter()
    {
        var version = ++_filterVersion;
        await Task.Delay(FilterDelay, Ports.Clock, CancellationToken.None).ConfigureAwait(true);
        if (version != _filterVersion)
        {
            return;   // a newer keystroke took over
        }

        await ApplyFilter(version).ConfigureAwait(true);
        Note = NoteText();
        Changed();
        FetchMoreForFilter();
    }

    private async Task ApplyFilter(int version)
    {
        var text = FilterText;
        var all = _all;
        var total = _next is null ? null : _total;
        var narrowed = await Task.Run(() => text.Length == 0 ? all.Narrow(text, total, Touched) : all.Narrow(text), CancellationToken.None).ConfigureAwait(true);
        if (version == _filterVersion && ReferenceEquals(all, _all))
        {
            FilterRuns++;
            Rows = narrowed;
        }
    }
}
