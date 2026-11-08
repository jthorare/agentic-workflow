using Aw.Sindri.Ui.ViewModels.Item;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The sheet's Back and what keeps its edge tab and name current (<see cref="ShellViewModel"/>, continued).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Back: closes the sheet like Esc does, after the question a Linear issue with unsaved edits asks.</summary>
    internal void Back()
    {
        if (IsIssueSheet && IsSheetOpen && IssueSheet.Leaving(then: Back))
        {
            return;   // unsaved edits on a Linear issue: the question comes first, and Discard closes the sheet
        }

        CloseSheet();
    }

    // Everything the sheet's names need once the item sheet exists (ShellViewModel's constructor calls this one line).
    private void InitSheetNames()
    {
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(CurrentPage), StringComparison.Ordinal))
            {
                Changed(nameof(IsEdgeTabShown));
            }
        };
        Item.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ItemSheetViewModel.Title), StringComparison.Ordinal))
            {
                Changed(nameof(EdgeTabText), nameof(SheetName), nameof(EdgeTabName));
            }
        };
    }
}
