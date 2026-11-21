using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The setup checklist in the shell (<see cref="ShellViewModel"/>, continued; setup spec §2.4): the list is read with every event, whatever page is shown, because Home's line
/// is on Home and a step can reopen; Home's line opens Settings at the checklist; and Home hides a setup row
/// while its line stands for them (Needs you elsewhere and the tray keep every row).
/// </summary>
internal sealed partial class ShellViewModel
{
    private const string SetupRowKind = "setup";

    /// <summary>The setup checklist behind Settings' first section and Home's line.</summary>
    public SetupViewModel Setup { get; }

    private static bool OpensInApp(string action) => action switch
    {
        _ => false,
    };

    private void InitSetup()
    {
        Health.Follow(Setup.Refresh);
        Health.SetupRequested += (_, _) => GoTo(PageId.Settings);
        Setup.ChecklistRequested += (_, _) => GoTo(PageId.Settings);
        Setup.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(SetupViewModel.IsHomeLineShown), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(SetupViewModel.HomeStepId), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(SetupViewModel.HasAnswered), StringComparison.Ordinal))
            {
                UpdateNeedsYou();
                Reattend();   // the line's step decides which setup row the card drops, and the summary counts the same rows (ShownOnHome)
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

    // The one rule for "this setup row is already on screen in Home's setup line": the title, badge, tray and announcement skip it, and Home's list drops it.
    private bool IsNamedByHomeLine(NeedsYouEntry row) =>
        Setup.IsHomeLineShown && string.Equals(row.Kind, SetupRowKind, StringComparison.Ordinal) && string.Equals(row.Id, SetupRowKind + ":" + Setup.HomeStepId, StringComparison.Ordinal);

    // Home's list: the decisions filter, and no setup row for the step Home's line names (setup-once spec 5.7: a row is hidden only for a step the line draws; every other setup row, like Approve all workflows, stays).
    // The inbox lists every row, whatever Home's filter says and whatever the line names.
    private bool ShownOnHome(NeedsYouEntry row) =>
        IsNeedsYouInbox
        || ((HomeFilter != HomeFilter.Decisions || row.DecisionId is not null) && !IsNamedByHomeLine(row));

    /// <summary>
    /// Esc on Settings over the Not running card: back to the page it was opened from, so the card shows again. While
    /// connected Settings is a page like any other, and Esc leaves it where it is.
    /// </summary>
    /// <returns>True when it left Settings.</returns>
    internal bool LeaveSettings()
    {
        if (!IsSettings || !IsNotRunning)
        {
            return false;
        }

        GoTo(_beforeSettings);
        return true;
    }
}
