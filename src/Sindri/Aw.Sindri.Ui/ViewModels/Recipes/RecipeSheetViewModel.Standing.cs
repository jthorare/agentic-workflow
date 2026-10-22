using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>
/// The recipe sheet's standing (<see cref="RecipeSheetViewModel"/>, continued; recipes spec §2): the recipe read does not serve where the workflow stands,
/// the list does, so the sheet reads the list once the recipe is shown. A list that can't be read leaves the sheet as it was, with no standing.
/// Approving is the terminal's: the sheet shows the command and copies it, and sends nothing.
/// </summary>
internal sealed partial class RecipeSheetViewModel
{
    /// <summary>The standing word ("Approved", "Not approved", "Revoked"); empty until the list answers.</summary>
    public string StandingText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The standing as a chip; null until the list answers.</summary>
    public StateChip? StandingChip { get; private set => Set(ref field, value); }

    /// <summary>True when the workflow is not approved or revoked: it can't start a run.</summary>
    public bool NeedsApproval { get; private set => Set(ref field, value); }

    /// <summary>The callout above the steps; empty when there is nothing to say.</summary>
    public string CalloutText { get; private set { Set(ref field, value); OnChanged(nameof(HasCallout)); } } = string.Empty;

    /// <summary>True when there is a callout.</summary>
    public bool HasCallout => CalloutText.Length > 0;

    /// <summary>Why the workflow is out of date, in words; empty when it is current.</summary>
    public IReadOnlyList<string> StaleReasons { get; private set => Set(ref field, value); } = [];

    /// <summary>True when there is a reason to show.</summary>
    public bool HasStaleReasons => StaleReasons.Count > 0;

    /// <summary>The line above the approve command.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ApproveHint => Labels.RecipeApproveHint;
#pragma warning restore CA1822, MA0041

    /// <summary>The terminal command that approves this workflow; empty unless it needs approval.</summary>
    public string ApproveCommand { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>One sentence on what the workflow does, when this build knows it.</summary>
    public string Description { get; private set { Set(ref field, value); OnChanged(nameof(HasDescription)); } } = string.Empty;

    /// <summary>True when there is a description.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>"Copy", or "Copied" after the command was copied.</summary>
    public string CopyLabel { get; private set => Set(ref field, value); } = Labels.Copy;

    /// <summary>Copies the approve command to the clipboard.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends when the clipboard answered.</returns>
    internal async Task CopyApprove(CancellationToken cancellationToken)
    {
        if (clipboard is null || ApproveCommand.Length == 0 || !await clipboard.SetText(ApproveCommand, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyLabel = Labels.Copied;
    }

    private async Task ReadStanding(Uri url, string recipe, string? hash, int generation, CancellationToken cancellationToken)
    {
        var list = await api.Recipes(url, cancellationToken).ConfigureAwait(true);
        if (generation != _generation || !list.IsSuccess)
        {
            return;   // another recipe took over, or the list can't be read: no standing is better than a wrong one
        }

        var listed = list.Value.Recipes.Where(r => string.Equals(r.Id, recipe, StringComparison.Ordinal)).ToList();
        var match = listed.Find(r => hash is not null && r.Hash.Contains(hash, StringComparison.OrdinalIgnoreCase)) ?? (hash is null ? listed.FirstOrDefault() : null);
        Listed(match is null ? null : new RecipeRowViewModel(match));
    }

    private void Listed(RecipeRowViewModel? row)
    {
        if (row is null)
        {
            ClearStanding();
            return;
        }

        StandingText = row.StandingText;
        StandingChip = row.StandingChip;
        NeedsApproval = row.Standing != RecipeStanding.Approved;
        StaleReasons = row.StaleReasons;
        OnChanged(nameof(HasStaleReasons));
        CalloutText = row.Standing switch
        {
            RecipeStanding.NotApproved => Labels.RecipeNotApprovedCallout,
            RecipeStanding.Revoked => Labels.RecipeRevokedCallout,
            RecipeStanding.Approved => row.IsOutOfDate ? Labels.RecipeOutOfDateCallout : string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(row), row.Standing, message: null),
        };
        ApproveCommand = NeedsApproval ? Labels.RecipeApproveCommand(row.Id) : string.Empty;
    }

    private void ClearStanding()
    {
        (StandingText, CalloutText, ApproveCommand, CopyLabel) = (string.Empty, string.Empty, string.Empty, Labels.Copy);
        (StandingChip, NeedsApproval, StaleReasons) = (null, false, []);
        OnChanged(nameof(HasStaleReasons));
    }
}
