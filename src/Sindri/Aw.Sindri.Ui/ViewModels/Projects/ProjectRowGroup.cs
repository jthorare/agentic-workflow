using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One group of rows as the rules lay it out: its key (a health level, a state, a repo), its head, and its projects in order.</summary>
/// <param name="Key">The level, state or repo; <see cref="NoOpenPrs"/> for the projects with no open PR, empty for the one group with no head.</param>
/// <param name="Heading">The head's words; empty for the one group with no head.</param>
/// <param name="Cards">The projects, in order.</param>
internal sealed record ProjectRowGroup(string Key, string Heading, IReadOnlyList<ProjectCardView> Cards)
{
    /// <summary>The key of the group of projects with no open PR that is not a draft.</summary>
    internal const string NoOpenPrs = "none";
}
