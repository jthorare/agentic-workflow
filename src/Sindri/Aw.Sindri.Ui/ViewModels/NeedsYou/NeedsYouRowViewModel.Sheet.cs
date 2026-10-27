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

    private const string CompleteAction = "CompleteStep";
    private const string WorkflowRoutePrefix = "workflow/";

    /// <summary>The ready Complete action of an open desk task, which the sheet offers as a button; null for any other row, a Complete the app may not send, or a task with no instance to name.</summary>
    internal NeedsYouAction? SheetComplete =>
        Address.Length > 0 ? Entry.Actions.FirstOrDefault(a => a.IsReady && string.Equals(a.CatalogId, CompleteAction, StringComparison.Ordinal)) : null;

    /// <summary>True when the sheet shows the Complete button.</summary>
    public bool ShowsComplete => SheetComplete is not null;

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The words on the Complete button.</summary>
    public string CompleteLabel => Labels.Action(CompleteAction);
#pragma warning restore CA1822, MA0041

    /// <summary>The Complete button's accessible name: the words and the instance's address ("Complete ITM-.../dev-desk-round-trip/1").</summary>
    public string CompleteName => Labels.CommandName(CompleteLabel, Address);

    // The desk task's instance address, as its row's open route names it ("workflow/<address>"); empty for a row that opens elsewhere.
    private string Address => Entry.Open?.Route is { } route && route.StartsWith(WorkflowRoutePrefix, StringComparison.Ordinal) ? route[WorkflowRoutePrefix.Length..] : string.Empty;
}
