using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Reviews tab in the shell (<see cref="ShellViewModel"/>, continued; review page spec §3, §6.1): the ninth tab, its waiting-count badge and Home's waiting line.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Reviews list.</summary>
    public ReviewsPageViewModel Reviews { get; private set; }

    /// <summary>True on Reviews.</summary>
    public bool IsReviews => CurrentPage == PageId.Reviews;

    /// <summary>Home's line under the Needs-you rows ("2 PRs waiting on you"); null when none wait. It opens Reviews.</summary>
    public string? ReviewsWaitingLine { get; private set; }

    [MemberNotNull(nameof(Reviews))]
    private void InitReviews() => Reviews = new ReviewsPageViewModel(_api, _clock);
}
