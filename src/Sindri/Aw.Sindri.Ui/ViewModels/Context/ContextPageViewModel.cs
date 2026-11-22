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
        Ports.Follow(FollowInOrder);
    }

    // The page's reads after every event, one after another in the order they were added: the shell runs the followed views
    // side by side (#2775), but these read each other's state (the rules line reads the whole map's answer, the whole map the build's state).
    private readonly List<Func<Uri, CancellationToken, Task>> _follows = [];

    private void Follow(Func<Uri, CancellationToken, Task> read) => _follows.Add(read);

    private async Task FollowInOrder(Uri api, CancellationToken cancellationToken)
    {
        foreach (var read in _follows)
        {
            await read(api, cancellationToken).ConfigureAwait(true);
        }
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
