using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Context.Layout;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>
/// The run page (F3 spec 3, slice 3): one run of a line workflow as a canvas of stage cards joined by edges, the runs of the same workflow
/// beside it, and the run's header. It reads the line, its recipe, the child of each stage and the runs list through the API, places the
/// cards with <see cref="RunCanvasLayout"/> and rereads on an instance change. Read-only: the inspector (slice 4) and the failed states
/// (slice 5) come later; <see cref="SelectedKey"/> is what the inspector will show.
/// </summary>
internal sealed partial class RunPageViewModel(ISindriApi api, CatalogViewModel catalog, IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    private const string CancelAction = "ApplyCommand:cancel";

    private Uri? _url;
    private string? _id;
    private int _generation;
    private string _state = string.Empty;   // the state the view served last, to compare with a frame's
    private HashSet<string> _watched = new(StringComparer.Ordinal);

    /// <summary>Raised when the page asks to leave for another place (a run of the list, See its context, Back).</summary>
    internal event EventHandler<OpenRequestedEventArgs>? LinkRequested;

    /// <summary>Raised when the header's Back button asks to leave the run for the Workflows list.</summary>
    internal event EventHandler? BackRequested;

    /// <summary>Raised when the canvas should move to a view (open at the active stage, centre, fit).</summary>
    internal event EventHandler<ValueEventArgs<RunFocus>>? ViewRequested;

    /// <summary>Where the read stands.</summary>
    public RunPageState Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            foreach (var name in (string[])[nameof(IsLoading), nameof(IsShown), nameof(IsProblem)])
            {
                OnChanged(name);
            }
        }
    }

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == RunPageState.Loading;

    /// <summary>True while a run is shown.</summary>
    public bool IsShown => Phase == RunPageState.Shown;

    /// <summary>True while a refusal, a missing run or an unreadable answer is shown.</summary>
    public bool IsProblem => Phase is RunPageState.NotFound or RunPageState.Problem;

    /// <summary>The run's address.</summary>
    public string Address { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The sentence a refusal or an unreadable answer shows.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The run's title.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The run's reference (its address), which a click copies.</summary>
    public string Reference { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Started 9h 35m ago".</summary>
    public string StartedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the run is, for the header's badge.</summary>
    public RunNodeState HeaderState { get; private set => Set(ref field, value); }

    /// <summary>The badge's word.</summary>
    public string HeaderWord { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The badge's mark.</summary>
    public string HeaderIcon { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The brush key of the badge's glyph and word.</summary>
    public string HeaderBrush { get; private set => Set(ref field, value); } = "Brush.InkSubtle";

    /// <summary>The brush key of the badge's fill.</summary>
    public string HeaderSoftBrush { get; private set => Set(ref field, value); } = "Brush.Surface2";

    /// <summary>The Cancel run control, or null when the run does not list the command.</summary>
    public ControlViewModel? Cancel { get; private set => Set(ref field, value); }

    /// <summary>The Copy of the reference.</summary>
    public CopyAction? ReferenceCopy { get; private set => Set(ref field, value); }

    /// <summary>The header's Back button words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string BackText => Labels.RunBack;

    /// <summary>The runs sidebar's eyebrow.</summary>
    public string RunsTitle => Labels.RunsTitle;

    /// <summary>The runs search box's hint.</summary>
    public string RunsSearch => Labels.RunsSearch;

    /// <summary>The note under the runs list.</summary>
    public string RunsOpenOnly => Labels.RunsOpenOnly;

    /// <summary>The loading heading.</summary>
    public string LoadingText => Labels.RunLoading;
#pragma warning restore CA1822, MA0041

    /// <summary>The cards, in the line's own order.</summary>
    public IReadOnlyList<RunCardViewModel> Cards { get; private set => Set(ref field, value); } = [];

    /// <summary>The label pills.</summary>
    public IReadOnlyList<RunPillViewModel> Pills { get; private set => Set(ref field, value); } = [];

    /// <summary>The edges for the edges layer.</summary>
    internal IReadOnlyList<MapEdge> Edges { get; private set => Set(ref field, value); } = [];

    /// <summary>The world's rectangle: the cards, padded.</summary>
    internal MapRect World { get; private set => Set(ref field, value); }

    /// <summary>The line under the canvas while the engine serves no branches; empty when it does.</summary>
    public string BranchesNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is a note under the canvas.</summary>
    public bool HasBranchesNote => BranchesNote.Length > 0;

    /// <summary>The card the run is at (the stage it opens on), or null before a read.</summary>
    public string? ActiveNodeId { get; private set => Set(ref field, value); }

    /// <summary>The reading order of the steps: stage and step ids, the way J and K walk.</summary>
    internal IReadOnlyList<string> ReadingOrder { get; private set; } = [];

    /// <summary>Opens a run: resolves the address when no id is known, then reads.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="address">The run's address.</param>
    /// <param name="id">Its id when the caller knows it; null otherwise.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the page shows the answer.</returns>
    internal Task Open(Uri url, string address, string? id, CancellationToken cancellationToken)
    {
        if (!string.Equals(address, Address, StringComparison.Ordinal))
        {
            Clear();
            Address = address;
            _id = null;
            Phase = RunPageState.Loading;
        }

        _id = id ?? _id;
        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Reads the run shown again (on every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the page shows the answer.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Address.Length == 0 ? Task.CompletedTask : Read(cancellationToken);
    }

    /// <summary>Reads the run again when a change names it or one of its stages' children (or for any change when <paramref name="change"/> is null).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="change">The frame, or null to read whatever is shown.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>False when the line still is not in the frame's state; true when it is, or when the change is not about this run.</returns>
    internal async Task<bool> Catch(Uri url, InstanceChange? change, CancellationToken cancellationToken)
    {
        if (Address.Length == 0 || (change is not null && !_watched.Contains(change.Instance)))
        {
            return true;   // nothing shown, or another run: nothing to read or wait for
        }

        _url = url;
        await Read(cancellationToken).ConfigureAwait(true);
        return change is null || !string.Equals(change.Instance, Address, StringComparison.Ordinal) || (Phase == RunPageState.Shown && string.Equals(_state, change.State, StringComparison.Ordinal));
    }

    /// <summary>Asks to go to another place (the runs list, See its context).</summary>
    /// <param name="link">The place.</param>
    internal void Go(DeepLink link) => LinkRequested?.Invoke(this, new OpenRequestedEventArgs(link));

    /// <summary>Copies the reference.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyReference(CancellationToken cancellationToken) => ReferenceCopy?.Run(Reference, cancellationToken) ?? Task.CompletedTask;

    /// <summary>Leaves the run for the Workflows list.</summary>
    internal void Back() => BackRequested?.Invoke(this, EventArgs.Empty);

    private void Raise(RunFocus focus) => ViewRequested?.Invoke(this, new ValueEventArgs<RunFocus>(focus));

    private void Clear()
    {
        _generation++;
        (Title, Reference, StartedText, ProblemText, BranchesNote, HeaderWord, HeaderIcon, _state) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (Cards, Pills, Edges, ActiveNodeId, Cancel, ReferenceCopy) = ([], [], [], null, null, null);
        (World, ReadingOrder, SelectedKey, _opened, _openPending) = (default, [], null, false, false);
        _watched = new(StringComparer.Ordinal);
        ClearRuns();
        ResetInspector();
        Phase = RunPageState.Empty;
    }

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        var generation = ++_generation;
        if (_id is null)
        {
            if (await Resolve(url, Address, generation, cancellationToken).ConfigureAwait(true) is not { } resolved)
            {
                return;
            }

            _id = resolved;
        }

        var answer = await api.Instance(url, _id, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another run, took over
        }

        if (answer.IsError)
        {
            Failed(answer.Error);
            return;
        }

        var line = answer.Value;
        var (recipe, children, runs) = await ReadAround(url, line, cancellationToken).ConfigureAwait(true);
        if (generation == _generation)
        {
            Show(line, recipe, children, runs);
            await ReadWhatWentWrong(url, _failureAt, cancellationToken).ConfigureAwait(true);
        }
    }

    // The recipe, each stage's child and the runs list are read together; any of them failing leaves its part to the fallback.
    private async Task<(RecipeView? Recipe, Dictionary<string, InstanceDetail> Children, IReadOnlyList<InstanceSummary> Runs)> ReadAround(Uri url, InstanceDetail line, CancellationToken cancellationToken)
    {
        var recipe = api.Recipe(url, line.Recipe.Id, hash: null, cancellationToken);
        var list = api.Instances(url, new InstanceQuery(Root: true, Open: true, Recipe: line.Recipe.Id), cancellationToken);
        var kids = line.Children.Select(g => g.Rows.FirstOrDefault(r => !string.Equals(r.Chip.Word, "DONE", StringComparison.OrdinalIgnoreCase)) ?? (g.Rows.Count > 0 ? g.Rows[^1] : null))
            .OfType<ChildRow>().DistinctBy(r => r.Id, StringComparer.Ordinal).ToList();
        var reads = await Task.WhenAll(kids.Select(async r => (r.Id, Answer: await api.Instance(url, r.Id, cancellationToken).ConfigureAwait(true)))).ConfigureAwait(true);
        var children = reads.Where(r => r.Answer.IsSuccess).ToDictionary(r => r.Id, r => r.Answer.Value, StringComparer.Ordinal);
        var recipeAnswer = await recipe.ConfigureAwait(true);
        var listAnswer = await list.ConfigureAwait(true);
        return (recipeAnswer.IsSuccess ? recipeAnswer.Value : null, children, listAnswer.IsSuccess ? listAnswer.Value.Instances : []);
    }
}
