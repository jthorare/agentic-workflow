using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Agents page, its session sheet and its counts in the shell (<see cref="ShellViewModel"/>, continued; U11a). T0 owns this file;
/// each builder fills one hook in a partial of its own: InitAgentsPage in ShellViewModel.AgentsPage.cs (T1),
/// InitAgentSheet in ShellViewModel.AgentSheet.cs (T3, T4) and InitAgentCounts in ShellViewModel.AgentCounts.cs (T5).
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Agents page's list.</summary>
    public AgentsPageViewModel Agents { get; private set; }

    /// <summary>The session sheet.</summary>
    public SessionSheetViewModel AgentSheet { get; private set; }

    // ShellViewModel's constructor calls this one line: both view models exist, then each builder's hook wires its own part.
    [MemberNotNull(nameof(Agents), nameof(AgentSheet), nameof(Jobs), nameof(JobSheet))]
    private void InitAgents(IClipboard clipboard)
    {
        Agents = new AgentsPageViewModel(_api, clipboard, _clock, Health.Catalog);
        AgentSheet = new SessionSheetViewModel(_api);
        InitAgentsPage();
        InitAgentSheet();
        InitAgentCounts();
        InitJobs();   // the Jobs page is not an Agents page, but it rides the same constructor line: ShellViewModel.cs is at its size cap
    }

    // The page's wiring: follow the control plane while the page is visible, the poll, Copy (T1).
    partial void InitAgentsPage();

    // The sheet's wiring: the row's open request, the re-read while open, Esc back to the row (T3, T4).
    partial void InitAgentSheet();

    // The counts' wiring: the tab's count and the tray line from the status (T5).
    partial void InitAgentCounts();

    /// <summary>True when the sheet's subject is a session.</summary>
    public bool IsSessionSheet => SheetLink?.Kind == DeepLinkKind.Session;

    // The session part of opening the sheet (OnSheetOpened): the sheet names its session at once and reads it (T3 implements it, and notifies IsSessionSheet).
    partial void OnAgentSheetOpened(DeepLink link);

    // The Agents page's own keys (J / K, Enter, O, Y, E) belong to the page and never to the sheet.
    private bool AgentsKeyPressed(string key, bool inSheet) => !inSheet && IsAgents && Agents.Press(key);
}
