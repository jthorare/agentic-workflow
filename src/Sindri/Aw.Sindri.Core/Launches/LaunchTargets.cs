using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Launches;

/// <summary>What this box can launch, as 'GET /launch/targets' serves it (UI spec E-3, screens spec E-28): the newest 'launch.probed' and the budgets.</summary>
/// <param name="Probed">True once a probe has been recorded; before that the lists are empty.</param>
/// <param name="ProbedAt">When the newest probe was recorded.</param>
/// <param name="Terminal">The configured viewer.</param>
/// <param name="Host">The session host an attended launch uses.</param>
/// <param name="Targets">Every catalog target, built-ins first.</param>
/// <param name="Repos">Every profile repo.</param>
/// <param name="BudgetTotal">The budget fields over all targets.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record LaunchTargets(bool Probed, DateTimeOffset? ProbedAt, TerminalFacts? Terminal, HostFacts? Host, IReadOnlyList<LaunchTargetRow> Targets, IReadOnlyList<RepoFacts> Repos, BudgetFacts BudgetTotal, AsOf AsOf);
