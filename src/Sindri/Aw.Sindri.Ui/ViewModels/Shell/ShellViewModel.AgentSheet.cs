using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The session sheet in the shell (<see cref="ShellViewModel"/>, continued; U11a T3, T4): a session link or a row's request opens it, it re-reads while open, and Esc knows the row.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The session whose Agents row opened the sheet (its ref), where Esc returns focus; null when it was opened some other way.</summary>
    internal string? OpenerSession { get; private set; }

    // A session or job sheet names its subject as the sheet does ("Session <provider> <title or id8>"), not by the ref, once the read has drawn.
    private string NamedSubject =>
        SheetLink?.Kind == DeepLinkKind.Session && AgentSheet.Heading.Length > 0 ? AgentSheet.Heading
        : SheetLink?.Kind == DeepLinkKind.Job && JobSheet.Heading.Length > 0 ? JobSheet.Heading
        : SheetSubject;

    partial void InitAgentSheet()
    {
        AgentSheet.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(SessionSheetViewModel.Heading), StringComparison.Ordinal))
            {
                Changed(nameof(SheetName), nameof(EdgeTabName));
            }
        };
        AgentSheet.Wire(Health.Catalog, _clock);
        Health.Follow((url, token) => IsSessionSheet && IsSheetOpen ? AgentSheet.Refresh(url, token) : Task.CompletedTask);
        InitAgentStep();
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
