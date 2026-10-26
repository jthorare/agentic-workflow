using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The session sheet in the shell (<see cref="ShellViewModel"/>, continued; U11a T3, T4): a session link or a row's request opens it, it re-reads while open, and Esc knows the row.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The session whose Agents row opened the sheet (its ref), where Esc returns focus; null when it was opened some other way.</summary>
    internal string? OpenerSession { get; private set; }

    // A sheet names its subject as it reads once drawn ("Session <provider> <title or id8>", the item's or run's title), not by the ref.
    private string NamedSubject => SheetHeading() is { Length: > 0 } heading ? heading : SheetSubject;

#pragma warning disable IDE0072 // the other kinds have no heading of their own
    private string SheetHeading() => SheetLink?.Kind switch
    {
        DeepLinkKind.Session => AgentSheet.Heading,
        DeepLinkKind.Job => JobSheet.Heading,
        DeepLinkKind.Item => Item.Title,
        DeepLinkKind.Workflow => Instance.Title,
        _ => string.Empty,
    };
#pragma warning restore IDE0072

    partial void InitAgentSheet()
    {
        AgentSheet.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(SessionSheetViewModel.Heading), StringComparison.Ordinal))
            {
                Changed(nameof(SheetTitle), nameof(SheetName), nameof(EdgeTabName));
            }
        };
        AgentSheet.Wire(Health.Catalog, _clock);
        Health.Follow((url, token) => IsSessionSheet && IsSheetOpen ? AgentSheet.Refresh(url, token) : Task.CompletedTask);
        InitAgentStep();
        AgentSheet.EndedResumeOf = Agents.ResumeOf;
        Agents.WorkflowsRequested += (_, _) => GoTo(PageId.Workflows);
        Agents.OpenRequested += (_, e) =>
        {
            OpenSheet(e.Link);
            OpenerSession = e.Link.Subject;   // Esc comes back to this row, after the sheet's own opening cleared it
        };
    }

    partial void OnAgentSheetOpened(DeepLink link)
    {
        OpenerSession = null;
        Changed(nameof(IsSessionSheet));
        if (link.Kind != DeepLinkKind.Session || link.Subject is not { } subject)
        {
            return;
        }

        AgentSheet.Show(subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = AgentSheet.Open(url, subject, CancellationToken.None);
        }
    }
}
