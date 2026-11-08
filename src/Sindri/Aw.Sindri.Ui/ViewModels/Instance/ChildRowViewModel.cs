using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// One child instance of the children panel (screens §4.2): the served chip, title, state, round, agent and age (blank while the engine
/// serves none), the park it mirrors to its parent, and the actions it serves. Its title opens the child's frame by its id; Open opens its
/// item's sheet. Nothing here sends: the launch of the item's agent stays pending (P10a) and the other actions wait for U8b.
/// </summary>
internal sealed class ChildRowViewModel : ObservableObject
{
    private const string OpenItemAction = "OpenItem";

    private readonly ChildRow _row;
    private readonly string _still;

    internal ChildRowViewModel(ChildRow row, IClipboard clipboard, TimeProvider clock, CatalogViewModel? catalog = null)
    {
        _row = row;
        ItemId = row.ItemId ?? string.Empty;
        Title = FactText.Scrub(row.Title);
        Glyph = StageCellViewModel.Mark(row.Glyph);
        RoundText = row.Round is { } round ? Labels.RoundOf(round) : string.Empty;
        Agent = FactText.Scrub(row.Agent ?? string.Empty);
        _still = string.Join(" · ", new[] { Labels.ChildStateWords(row.State), RoundText, Agent }.Where(p => p.Length > 0));
        (Age, Meta) = (string.Empty, string.Empty);
        Update(row);
        (MirroredText, BadgeText) = Park(row);
        IsDone = row.Glyph == Api.Workflow.StateGlyph.Done;
        CanOpenItem = ItemId.Length > 0 && row.Actions.Contains(OpenItemAction, StringComparer.Ordinal);
        OpenName = Labels.OpenItemName(ItemId);
        LaunchChip = CanOpenItem && !IsDone ? Labels.AvailableWithP10a : string.Empty;
        Actions = IsDone ? [] : [.. row.Actions.Where(a => !a.StartsWith(OpenItemAction, StringComparison.Ordinal)).Select(a => new ChildActionViewModel(a, row.Address, clipboard, clock, catalog?.Row(a)))];
        OpenFrameName = Labels.OpenInstanceName(Title, row.Address);
    }

    /// <summary>Raised when the row asks to open the child's frame.</summary>
    internal event EventHandler<FrameOpenEventArgs>? Opened;

    /// <summary>Raised when the row asks to open the child's item sheet.</summary>
    internal event EventHandler<ItemOpenEventArgs>? ItemRequested;

    /// <summary>The child's opaque id.</summary>
    public string Id => _row.Id;

    /// <summary>The child's address.</summary>
    public string Address => _row.Address;

    /// <summary>The child's item, or empty.</summary>
    public string ItemId { get; }

    /// <summary>The title as served.</summary>
    public string Title { get; }

    /// <summary>The served chip.</summary>
    public StateChip Chip => _row.Chip;

    /// <summary>The mark of the child's progress.</summary>
    public string Glyph { get; }

    /// <summary>The state as served.</summary>
    public string State => _row.State;

    /// <summary>"r1/3", or empty.</summary>
    public string RoundText { get; }

    /// <summary>The agent as served, or empty.</summary>
    public string Agent { get; }

    /// <summary>"6m".</summary>
    public string Age { get; private set => Set(ref field, value); }

    /// <summary>The state, round, agent and age.</summary>
    public string Meta { get; private set => Set(ref field, value); }

    /// <summary>The park the engine mirrors here, in words; empty when none.</summary>
    public string MirroredText { get; }

    /// <summary>True when the row has a mirrored park.</summary>
    public bool HasMirrored => MirroredText.Length > 0;

    /// <summary>The engine's own badge on the child when it is not mirrored; empty otherwise.</summary>
    public string BadgeText { get; }

    /// <summary>True when the row has an engine badge.</summary>
    public bool HasBadge => BadgeText.Length > 0;

    /// <summary>True when the child is done.</summary>
    public bool IsDone { get; }

    /// <summary>True when the child has an item to open.</summary>
    public bool CanOpenItem { get; }

    /// <summary>Open's accessible name.</summary>
    public string OpenName { get; }

    /// <summary>The chip that says the launch arrives with P10a; empty for a done child.</summary>
    public string LaunchChip { get; }

    /// <summary>True when the launch chip shows.</summary>
    public bool HasLaunchChip => LaunchChip.Length > 0;

    /// <summary>The other actions the child serves; none for a done child.</summary>
    public IReadOnlyList<ChildActionViewModel> Actions { get; }

    /// <summary>True when there are other actions.</summary>
    public bool HasActions => Actions.Count > 0;

    /// <summary>The title button's accessible name.</summary>
    public string OpenFrameName { get; }

    /// <summary>What the row draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('|', Id, Address, Title, Chip, Glyph, _still, MirroredText, BadgeText, ItemId, string.Join(',', Actions.Select(a => $"{a.Label}={a.Name}")));

    /// <summary>Takes a re-read of the same row: only the age moves, and the row (with the control that has focus) stays.</summary>
    /// <param name="row">The row as served now; its <see cref="Signature"/> equals this one's.</param>
    internal void Update(ChildRow row)
    {
        Age = Labels.RowAge(TimeSpan.FromSeconds(row.AgeSeconds));
        Meta = string.Join(" · ", new[] { _still, Age }.Where(p => p.Length > 0));
    }

    /// <summary>Opens the child's frame by its id.</summary>
    public void OpenFrame() => Opened?.Invoke(this, new FrameOpenEventArgs(Address, Id));

    // A park mirrored to the parent reads with "mirrored here"; an engine badge that is not mirrored stands alone.
    private static (string Mirrored, string Badge) Park(ChildRow row) =>
        row.MirroredPark is { } park ? ($"{Labels.CellBadge(park.Kind, park.Reason)} · {Labels.MirroredHere}", string.Empty)
        : row.EngineOwned is { } owned ? (string.Empty, Labels.CellBadge(owned.Kind, owned.Reason))
        : (string.Empty, string.Empty);

    /// <summary>Opens the child's item sheet.</summary>
    public void OpenItem()
    {
        if (CanOpenItem)
        {
            ItemRequested?.Invoke(this, new ItemOpenEventArgs(ItemId));
        }
    }
}
