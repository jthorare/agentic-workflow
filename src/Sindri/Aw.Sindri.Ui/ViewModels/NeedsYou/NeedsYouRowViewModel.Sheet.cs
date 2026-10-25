namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A Needs-you row (<see cref="NeedsYouRowViewModel"/>, continued): how its sheet splits the lines up front from the bookkeeping behind Details.</summary>
internal sealed partial class NeedsYouRowViewModel
{
    /// <summary>The lines the sheet shows up front: the plain description and anything the person acts on.</summary>
    public IReadOnlyList<SheetDetail> PrimaryDetails => [.. Details.Where(d => !d.Secondary)];

    /// <summary>The bookkeeping lines (a hash, the approve command) the sheet keeps behind a closed Details.</summary>
    public IReadOnlyList<SheetDetail> MoreDetails => [.. Details.Where(d => d.Secondary)];

    /// <summary>True when the row has bookkeeping lines.</summary>
    public bool HasMoreDetails => Details.Any(d => d.Secondary);

    /// <summary>The disclosure's heading.</summary>
    public string MoreHeading { get; } = Labels.SheetDetails;

    /// <summary>True while the sheet's Details is open; closed until asked for.</summary>
    public bool DetailsOpen { get; set => Set(ref field, value); }

    /// <summary>True when the served command shows under the lines: not when Details already holds it.</summary>
    public bool ShowsCliUpFront => Cli.Length > 0 && !HasMoreDetails;

    /// <summary>The ready Complete action of an open desk task, which the sheet offers as a button; null for any other row.</summary>
    internal NeedsYouAction? SheetComplete => null;

    /// <summary>True when the sheet shows the Complete button.</summary>
    public bool ShowsComplete => SheetComplete is not null;

    /// <summary>The words on the Complete button.</summary>
    public string CompleteLabel => string.Empty;

    /// <summary>The Complete button's accessible name: the words and the instance's address ("Complete ITM-.../dev-desk-round-trip/1").</summary>
    public string CompleteName => string.Empty;
}

