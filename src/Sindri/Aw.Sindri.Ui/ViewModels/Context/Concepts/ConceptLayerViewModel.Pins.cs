using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The concept layer (continued): its pin side, the pins read, the buttons and the status line (concept pins spec §3.6-§3.8).</summary>
internal sealed partial class ConceptLayerViewModel
{
    private PinsView? _pins;
    private bool _wasBuilding;
    private Action<PinSheetOpen>? _openPin;
    private Func<string> _repo = static () => string.Empty;
    private TimeZoneInfo _zone = TimeZoneInfo.Local;

    /// <summary>The layer's pin side: where the pin sheet opens, which repo's pins, the review hooks and the zone the retry time is shown in.</summary>
    /// <param name="openPin">Opens the Pin sheet; null while the host has none (every pin button is then hidden).</param>
    /// <param name="repo">The repo the layer shows; empty for the profile's only repo.</param>
    /// <param name="zone">The zone of the backoff's retry time; null for the local one.</param>
    internal void WirePins(Action<PinSheetOpen>? openPin, Func<string> repo, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(repo);
        (_openPin, _repo, _zone) = (openPin, repo, zone ?? TimeZoneInfo.Local);
    }

    /// <summary>Opens the needs-you review the setup step uses (a pinned card's Review and approve).</summary>
    internal Action? ReviewAndApprove { get; set; }

    /// <summary>Opens the held map's review (a pinned card's Open review); the button is hidden while there is none.</summary>
    internal Action? OpenReview { get; set; }

    /// <summary>Queues a map refresh again (a pinned card's Try again).</summary>
    internal Action? TryAgain { get; set; }

    /// <summary>Whether the server answered the pins read: every pin button and mark shows. False on a 404 or any failure (an older server).</summary>
    internal bool PinsServed => _pins is not null && _openPin is not null;

    /// <summary>Whether the layer head carries New pin.</summary>
    public bool ShowNewPin => PinsServed;

    /// <summary>Whether the layer head carries Add several; off until the Add several sheet is built.</summary>
    public bool ShowAddSeveral => PinsServed && SeveralAvailable;

    /// <summary>Whether the Add several sheet is built; the shell sets it when it is.</summary>
    internal bool SeveralAvailable { get; set; }

    /// <summary>The New pin button's words.</summary>
    public string NewPinText { get; } = Labels.PinNew;

    /// <summary>The Add several button's words.</summary>
    public string AddSeveralText { get; } = Labels.PinAddSeveral;

    /// <summary>The head line over the cards: what the pins cover, or that there are none; empty while the server has no pins.</summary>
    public string PinHeadText { get; private set; } = string.Empty;

    /// <summary>Whether the pin head line has words.</summary>
    public bool HasPinHead => PinHeadText.Length > 0;

    /// <summary>The layer's one status line, a polite live region: the map is rebuilding, or is ready.</summary>
    public string RebuildText { get; private set; } = string.Empty;

    /// <summary>Whether the status line has words.</summary>
    public bool HasRebuildText => RebuildText.Length > 0;

    /// <summary>The keys of the map's features, when the layer knows them, so a pin's features the map lost are called gone; null otherwise.</summary>
    internal IReadOnlySet<string>? MapFeatures { get; set; }

    /// <summary>The pins were read: cards take their mark, footer and buttons.</summary>
    /// <param name="view">The ok answer.</param>
    internal void ShowPins(PinsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _pins = view;
        var building = string.Equals(view.Rebuild.Phase, MapRebuildPhases.Building, StringComparison.Ordinal);
        if (building)
        {
            RebuildText = Labels.PinRebuildingText(view.Rebuild.Progress);
        }
        else if (_wasBuilding)
        {
            RebuildText = view.Rebuild.Failed is null ? Labels.PinMapReady : string.Empty;
        }

        _wasBuilding = building;
        Changed();
    }

    /// <summary>The server has no pins route, or the read failed: the layer is as it was, with no pin button.</summary>
    internal void PinsNotServed()
    {
        _pins = null;
        Changed();
    }

    /// <summary>Pin this: hands the shell the derived card.</summary>
    /// <param name="tile">The card.</param>
    internal void PinThis(ConceptTileViewModel tile) => Open(new PinSheetOpen(_repo(), tile, Pin: null, PinList));

    /// <summary>Edit pin: hands the shell the card's pin.</summary>
    /// <param name="tile">The card.</param>
    internal void EditPin(ConceptTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (tile.Card.Pin is { } pin)
        {
            Open(new PinSheetOpen(_repo(), Card: null, pin, PinList));
        }
    }

    /// <summary>New pin: the sheet from nothing.</summary>
    internal void NewPin() => Open(new PinSheetOpen(_repo(), Card: null, Pin: null, PinList));

    /// <summary>Add several: the sheet for a list.</summary>
    internal void AddSeveral() => Open(new PinSheetOpen(_repo(), Card: null, Pin: null, PinList, Several: true));

    /// <summary>The footer button of a card.</summary>
    /// <param name="tile">The card.</param>
    internal void RunAction(ConceptTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        var action = tile.Card.Action switch
        {
            PinCardAction.ReviewAndApprove => ReviewAndApprove,
            PinCardAction.OpenReview => OpenReview,
            PinCardAction.TryAgain => TryAgain,
            PinCardAction.None => null,
            _ => null,
        };
        action?.Invoke();
    }

    private IReadOnlyList<PinView> PinList => _pins?.Pins ?? [];

    private void Open(PinSheetOpen request) => _openPin?.Invoke(request);
}
