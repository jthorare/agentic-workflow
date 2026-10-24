using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The Files view (F3 spec 1, slice 4): one folder of the repo at a time from GET /sindri/context/filemap, each entry a
/// <see cref="FileRowViewModel"/>. The repo is the one the map status names; the newest read wins.
/// </summary>
internal sealed partial class FileMapViewModel(ContextPorts ports) : ObservableObject
{
    private string? _repo;
    private string _current = string.Empty;
    private int _generation;
    private int _filterVersion;
    private LazyFileRows _all = new(entries: [], symbols: null);
    private FileMapEntry[] _entries = [];
    private List<FileSymbolsEntry> _symbolFiles = [];
    private SymbolsView? _symbolsServed;
    private string? _next;
    private int? _total;
    private bool _fetching;

    /// <summary>How long typing must pause before the filter runs.</summary>
    internal static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>The page size asked for.</summary>
    internal const int PageSize = 200;

    /// <summary>The next page is read when the list reaches this near its read end.</summary>
    internal const int PrefetchAhead = 50;

    /// <summary>How many entries are read so far.</summary>
    internal int Loaded => _entries.Length;

    /// <summary>How many times the filter has run (a burst of keystrokes runs it once).</summary>
    internal int FilterRuns { get; private set; }

    /// <summary>What the stage reads through.</summary>
    internal ContextPorts Ports { get; } = ports;

    /// <summary>The symbols column of the folder shown (S-9); null before a read.</summary>
    internal SymbolsColumnViewModel? Symbols { get; private set; }

    /// <summary>The symbols column's heading.</summary>
    public string ColSymbols { get; } = Labels.ContextSymbolsColumn;

    /// <summary>The heading with where the names came from ("symbols · from 7c3e1a2", "· stale"): provenance in words, since a dimmed cell says it by colour alone. When the index cannot answer, each cell says why instead.</summary>
    public string SymbolsHeading => Symbols is { Unavailable: false } column ? Labels.Dots(ColSymbols, column.Caption) : ColSymbols;

    /// <summary>The first column's heading.</summary>
    public string ColFile { get; } = Labels.ContextFilesColFile;

    /// <summary>The second column's heading.</summary>
    public string ColFor { get; } = Labels.ContextFilesColFor;

    /// <summary>The third column's heading.</summary>
    public string ColIntent { get; } = Labels.ContextMapAreaIntent;

    /// <summary>The up button's words.</summary>
    public string UpWord { get; } = Labels.ContextFilesUp;

    /// <summary>The word while a folder is read.</summary>
    public string LoadingWord { get; } = Labels.ContextFilesLoading;

    /// <summary>The word for a folder with nothing in it.</summary>
    public string EmptyWord { get; } = Labels.ContextFilesEmpty;

    /// <summary>The filter box's hint.</summary>
    public string FilterHint { get; } = Labels.ContextFilesFilter;

    /// <summary>The filter box's text; the rows follow it once typing pauses.</summary>
    public string FilterText
    {
        get;
        set
        {
            var text = value ?? string.Empty;
            if (string.Equals(field, text, StringComparison.Ordinal))
            {
                return;
            }

            Set(ref field, text);
            _ = Refilter();
        }
    } = string.Empty;

    /// <summary>The rows of the folder shown, built as the list scrolls to them.</summary>
    public IReadOnlyList<FileRowViewModel> Rows { get; private set => Set(ref field, value); } = [];

    /// <summary>The folder shown, in words.</summary>
    public string PathText { get; private set => Set(ref field, value); } = Labels.ContextFilesRoot;

    /// <summary>Whether there is a folder above this one.</summary>
    public bool CanGoUp { get; private set => Set(ref field, value); }

    /// <summary>The totals line (files, in the map, suggested, not in the map).</summary>
    public string TotalsText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The note under the table, or empty.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's own words, or empty.</summary>
    public string Problem { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Whether a read is in flight.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); }

    /// <summary>Whether the table has rows.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Whether the folder was read and holds nothing.</summary>
    public bool IsEmptyFolder => !IsLoading && !HasProblem && Rows.Count == 0 && TotalsText.Length > 0;

    /// <summary>Whether there is a note under the table.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Whether there is a refusal to show.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>Reads a folder and shows it. Nothing is read when the control plane's address is not known.</summary>
    /// <param name="path">The folder (as served, ending in a slash), or null for the top.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal async Task Load(string? path, CancellationToken cancellationToken = default)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var generation = ++_generation;   // a read still in flight must not draw over this one
        IsLoading = true;
        Changed();
        try
        {
            await Read(api, path, generation, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            if (generation == _generation)
            {
                IsLoading = false;
                Changed();
            }
        }
    }

    private async Task Read(Uri api, string? path, int generation, CancellationToken cancellationToken)
    {
        var repo = _repo ?? await ReadRepo(api, cancellationToken).ConfigureAwait(true);
        if (generation != _generation || repo is null)
        {
            return;
        }

        _repo = repo;
        var read = await Ports.Api.FileMap(api, new FileMapQuery(repo, path, UnmappedOnly: false, Draft: false, Cursor: null, Limit: PageSize), cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        if (read.IsSuccess)
        {
            Show(read.Value);
        }
        else
        {
            Fail(read.Error);
        }
    }

    /// <summary>Reads the folder shown again, or the top when none is (the Files view was just opened).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal Task Opened(CancellationToken cancellationToken = default) => Load(_current.Length == 0 ? null : _current, cancellationToken);

    /// <summary>Opens a folder; a file does nothing.</summary>
    /// <param name="row">The row.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the folder is shown.</returns>
    internal Task Open(FileRowViewModel row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.IsDirectory ? Load(row.Path, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>Goes up one folder.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the folder is shown.</returns>
    internal Task Up(CancellationToken cancellationToken = default) => CanGoUp ? Load(Parent(_current), cancellationToken) : Task.CompletedTask;

    // "src/tabs/" -> "src/"; "src/" -> the top (null).
    private static string? Parent(string path)
    {
        var trimmed = path.TrimEnd('/');
        var cut = trimmed.LastIndexOf('/');
        return cut < 0 ? null : trimmed[..(cut + 1)];
    }

    // The map status names the repo the caller reads; a refusal (no map yet) is the file map's refusal too.
    private async Task<string?> ReadRepo(Uri api, CancellationToken cancellationToken)
    {
        var status = await Ports.Api.MapStatus(api, anchor: null, cancellationToken).ConfigureAwait(true);
        if (status.IsError)
        {
            Fail(status.Error);
            return null;
        }

        return status.Value.Version.Repo;
    }

    private void Show(FileMapView view)
    {
        _current = view.Path;
        _fetching = false;
        _entries = [.. view.Entries];
        _symbolFiles = [.. view.Symbols.Files ?? []];
        _symbolsServed = view.Symbols;
        _next = view.NextCursor;
        _total = view.Total;
        Problem = string.Empty;
        Symbols = new SymbolsColumnViewModel(view.Symbols);
        Rebuild();
        PathText = view.Path.Length == 0 ? Labels.ContextFilesRoot : view.Path;
        CanGoUp = view.Path.Length > 0;
        TotalsText = Labels.ContextFilesTotals(view.Totals.Files, view.Totals.Mapped, view.Totals.Suggested, view.Totals.Unmapped);
        Note = NoteText();
        FetchMoreForFilter();
    }

    private void Fail(Error error)
    {
        _all = new LazyFileRows(entries: [], symbols: null);
        _entries = [];
        _next = null;
        _total = null;
        Rows = _all;
        TotalsText = string.Empty;
        Note = string.Empty;
        Problem = error.Message;
    }

    private void Changed()
    {
        OnChanged(nameof(HasRows));
        OnChanged(nameof(IsEmptyFolder));
        OnChanged(nameof(HasNote));
        OnChanged(nameof(HasProblem));
        OnChanged(nameof(SymbolsHeading));
    }
}
