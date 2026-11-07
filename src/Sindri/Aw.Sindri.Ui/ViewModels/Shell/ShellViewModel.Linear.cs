using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Work page's Linear view in the shell (<see cref="ShellViewModel"/>, continued; Linear hub spec §3): it reads while Work is the page shown, and its Connect Linear button opens Settings.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Work page's Linear view: its switch, list and since-you-looked strip.</summary>
    public LinearWorkViewModel Linear { get; private set; }

    /// <summary>The Linear "New issue" dialog (`C` on the Linear view).</summary>
    public NewLinearIssueViewModel NewIssue { get; private set; }

    [MemberNotNull(nameof(Linear), nameof(NewIssue))]
    private void InitLinear()
    {
        Linear = new LinearWorkViewModel(_api, _store, _clock);
        NewIssue = new NewLinearIssueViewModel(_api, Health.ApiUrl, _clock);
        NewIssue.Created += (_, created) => Linear.SelectWhenShown(created.Value, NewIssue.CreatedWhileOpen);
        Health.Follow(Linear.Refresh);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsWork), StringComparison.Ordinal))
            {
                Linear.SetShown(IsWork);
            }
        };
        Linear.ConnectRequested += (_, _) => GoTo(PageId.Settings);
    }

    // L switches Work between Sindri and Linear from anywhere on the page; C creates an item, and on the Linear view opens the New issue dialog.
    // The row keys belong to the Linear rows on the Linear view (J / K select, Enter opens, / focuses the filter bar), never to the hidden queue; Y has no action there. In a sheet J, K and Enter go to the sheet.
    private bool LinearKeyPressed(string key, bool inSheet)
    {
        if (!IsWork)
        {
            return false;
        }

        if (string.Equals(key, "L", StringComparison.Ordinal))
        {
            Linear.Toggle();
            return true;
        }

        return Linear.IsLinear && LinearViewKey(key, inSheet);
    }

    // The keys of the Linear view itself.
    private bool LinearViewKey(string key, bool inSheet)
    {
        switch (key)
        {
            case "J" or "K" when !inSheet:
                Linear.Move(key == "J" ? 1 : -1);
                return true;
            case "C":
                OpenNewIssue();
                return true;
            case "Enter" when !inSheet:
                _ = Linear.OpenSelected();
                return true;
            case "/" when !inSheet:
                Linear.FocusFilters();
                return true;
            case "/" or "Y":
                return true;   // swallowed: / in a sheet, and Y, which has no action here
            default:
                return false;
        }
    }

    /// <summary>Opens the New issue dialog (`C` on the Linear view, or its header button); the draft a close left is offered again.</summary>
    internal void OpenNewIssue()
    {
        if (!NewIssue.IsOpen)
        {
            _ = NewIssue.Open(CancellationToken.None);
        }
    }
}
