using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Linear issue sheet in the shell (<see cref="ShellViewModel"/>, continued; Linear hub spec §3): a deep link, a Work → Linear row or strip row, or a review header chip opens it; Home and the tray say how many issues changed.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Linear issue sheet.</summary>
    public LinearIssueSheetViewModel IssueSheet { get; private set; }

    /// <summary>True when the sheet's subject is a Linear issue.</summary>
    public bool IsIssueSheet => SheetLink?.Kind == DeepLinkKind.Issue;

#pragma warning disable CA1822, MA0041 // red stub: the line arrives with the implementation
    /// <summary>Home's line ("3 new on your Linear issues"); null when none are new.</summary>
    public string? LinearNewLine => null;
#pragma warning restore CA1822, MA0041

    /// <summary>True when Home draws the Linear line.</summary>
    public bool HasLinearNewLine => LinearNewLine is not null;

    /// <summary>Home's Linear line: Work on the Linear view.</summary>
    internal void OpenLinear() => throw new NotSupportedException();

    [MemberNotNull(nameof(IssueSheet))]
    private void InitIssueSheet(ILauncher launcher) => IssueSheet = new LinearIssueSheetViewModel(_api, launcher, _clock);
}
