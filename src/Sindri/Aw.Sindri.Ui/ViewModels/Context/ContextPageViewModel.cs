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
        InitReview();
        InitFileMap();
        InitSide();
        InitStates();
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
        set => Set(ref field, value);
    } = string.Empty;
}
