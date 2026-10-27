using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The setup checklist in the shell (<see cref="ShellViewModel"/>, continued; setup spec §2.4): the list is read with every event, whatever page is shown, because Home's line
/// is on Home and a step can reopen; Home's line opens Settings at the checklist; a step's button opens approve-all or the profile's approval row; and Home hides a setup row
/// while its line stands for them (Needs you elsewhere and the tray keep every row).
/// </summary>
internal sealed partial class ShellViewModel
{
    private const string SetupRowKind = "setup";
    private const string ProfileDriftRow = "profile:drift";

    /// <summary>The setup checklist behind Settings' first section and Home's line.</summary>
    public SetupViewModel Setup { get; }

    // The app does approve-all itself, and opens the profile's approval row when Needs you has it; every other action is a command to copy until its form exists.
    private bool OpensInApp(string action) => action switch
    {
        ApproveAllViewModel.Action => true,
        "ApproveProfile" => _needsYou?.Rows.Any(r => string.Equals(r.Id, ProfileDriftRow, StringComparison.Ordinal)) == true,   // the unfiltered feed: Home's Decisions filter trims what NeedsYou lists
        _ => false,
    };

    private void InitSetup()
    {
        Health.Follow(Setup.Refresh);
        Setup.ChecklistRequested += (_, _) => GoTo(PageId.Settings);
        Setup.ActionRequested += (_, e) => OpenSetupAction(e.Action);
        Setup.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(SetupViewModel.IsHomeLineShown), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(SetupViewModel.HomeStepId), StringComparison.Ordinal))
            {
                UpdateNeedsYou();
            }
        };
        Health.Catalog.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal))
            {
                Setup.Rederive();
            }
        };
        NeedsYou.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(NeedsYouViewModel.Rows), StringComparison.Ordinal))
            {
                Setup.Rederive();
            }
        };
    }

    private void OpenSetupAction(string action)
    {
        if (string.Equals(action, ApproveAllViewModel.Action, StringComparison.Ordinal))
        {
            OpenApproveAll();
        }
        else if (string.Equals(action, "ApproveProfile", StringComparison.Ordinal))
        {
            Navigate(LinkOf(ProfileDriftRow));
        }
    }

    // Home's list: the decisions filter, and no setup row for the step Home's line names (setup-once spec 5.7: a row is hidden only for a step the line draws; every other setup row, like Approve all workflows, stays).
    // The inbox lists every row, whatever Home's filter says and whatever the line names.
    private bool ShownOnHome(NeedsYouEntry row) =>
        IsNeedsYouInbox
        || ((HomeFilter != HomeFilter.Decisions || row.DecisionId is not null)
            && !(Setup.IsHomeLineShown && string.Equals(row.Kind, SetupRowKind, StringComparison.Ordinal) && string.Equals(row.Id, SetupRowKind + ":" + Setup.HomeStepId, StringComparison.Ordinal)));
}
