using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The Context page (screens spec §4.4): a head, the lens stack and the side column. U9a0 only draws the skeleton, opened empty
/// with no read made; U9a owns this file from here on, and the seams (<see cref="Review"/>, <see cref="FileMap"/>,
/// <see cref="Node"/>, <see cref="Sources"/>) live in the partial files named for the stage that owns each.
/// </summary>
internal sealed partial class ContextPageViewModel : ObservableObject
{
    /// <summary>The page over the API.</summary>
    /// <param name="ports">What the page and its stages read through.</param>
    internal ContextPageViewModel(ContextPorts ports)
    {
        Ports = ports;
        MapSearch = new DrawnMapSearch(() => Map);
        InitReview();
        InitFileMap();
        InitSide();
        InitStates();
        InitWhole();
        InitRules();
        Concepts = new ConceptLayerViewModel(id => _ = OpenConcept(id));
        InitConcepts();
        InitPageSheet();
        Ports.Follow((api, cancellationToken) => InOrder(_follows, api, cancellationToken));
    }

    // The page's reads after every event, one after another in the order they were added: the shell runs the followed views
    // side by side (#2775), but these read each other's state (the rules line reads the whole map's answer, the whole map the build's state).
    private readonly List<Func<Uri, CancellationToken, Task>> _follows = [];

    private void Follow(Func<Uri, CancellationToken, Task> read) => _follows.Add(read);

    /// <summary>
    /// Runs the reads one after another. One that throws stops none after it: the first exception comes out once all have run, as the
    /// shell's own refresh reports a view that throws (a cancelled refresh still ends at once).
    /// </summary>
    /// <param name="reads">The reads, in order.</param>
    /// <param name="api">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that ends when every read has.</returns>
    internal static async Task InOrder(IReadOnlyList<Func<Uri, CancellationToken, Task>> reads, Uri api, CancellationToken cancellationToken)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? first = null;
        foreach (var read in reads)
        {
            try
            {
                await read(api, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // why: kept and thrown again below, once the reads after it have run
            catch (Exception e)
#pragma warning restore CA1031
            {
                first ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e);
            }
        }

        first?.Throw();
    }

    /// <summary>What the page and its stages read through.</summary>
    internal ContextPorts Ports { get; }

    /// <summary>The page's title.</summary>
    public string Title { get; } = Labels.Context;

    /// <summary>The head line: what map the page shows. Empty until something has been read; "no map" is for the served no-map case (U9a).</summary>
    public string Head { get; private set; } = string.Empty;

    /// <summary>Whether the head has words to show.</summary>
    public bool HasHead => Head.Length > 0;

    /// <summary>The anchor box's name.</summary>
    public string AnchorLabel { get; } = Labels.ContextAnchor;

    /// <summary>The anchor box's hint.</summary>
    public string AnchorHint { get; } = Labels.ContextAnchorHint;

    /// <summary>What is typed in the anchor box.</summary>
    public string AnchorText
    {
        get;
        set
        {
            Set(ref field, value);
            if (string.IsNullOrWhiteSpace(value))
            {
                _ = BackToWholeMap();   // an empty box is no search: the full map again
            }
        }
    } = string.Empty;

    /// <summary>The search over the map; the ranked full-text endpoint replaces it with one change.</summary>
    internal IMapSearch MapSearch { get; set; }
}
