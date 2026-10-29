using Aw.Kernel;
using Aw.Sindri.Core.Projects.Facts;

namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>What the Projects page is built from (projects page spec §6.2 steps 1, 2 and 4).</summary>
/// <param name="Attached">The 'project-attached' rows.</param>
/// <param name="Hidden">The 'project-hidden' rows.</param>
/// <param name="Facts">Context's answer, or its error.</param>
/// <param name="Freshness">The sources' sync state, or its error.</param>
/// <param name="LinearConfigured">Whether the profile configures a Linear source (state 4 tells "connect Linear" from "still syncing").</param>
public sealed record ProjectPageInputs(
    IReadOnlyList<ProjectAttachedRow> Attached,
    IReadOnlyList<ProjectHiddenRow> Hidden,
    Result<ProjectFactsSnapshot> Facts,
    Result<IReadOnlyList<ProjectSourceFreshness>> Freshness,
    bool LinearConfigured);
