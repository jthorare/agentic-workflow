using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Launches;

/// <summary>Folds the newest 'launch.probed' with the profile's budgets and today's spend into 'GET /launch/targets' (UI spec E-3). Pure.</summary>
public static class LaunchTargetsProjection
{
    /// <summary>Builds the view.</summary>
    /// <param name="probed">The newest probe, or null before the first.</param>
    /// <param name="probedAt">When it was recorded.</param>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="spend">Today's spend per agent id; an id without an entry has no usage reader.</param>
    /// <param name="asOf">How fresh the views are.</param>
    /// <returns>The view.</returns>
    public static LaunchTargets Project(LaunchProbed? probed, DateTimeOffset? probedAt, AgentSettings? agents, IReadOnlyDictionary<string, AgentSpend> spend, AsOf asOf)
    {
        ArgumentNullException.ThrowIfNull(spend);
        ArgumentNullException.ThrowIfNull(asOf);
        if (probed is null)
        {
            return new LaunchTargets(Probed: false, ProbedAt: null, Terminal: null, Host: null, [], [], Budgets.Total(agents, []), asOf);
        }

        var rows = new List<LaunchTargetRow>(probed.Targets.Count);
        foreach (var target in probed.Targets)
        {
            var found = spend.TryGetValue(target.Id.Value, out var used);
            rows.Add(new LaunchTargetRow(target, Budgets.For(agents, target.Id, found ? used : null)));
        }

        return new LaunchTargets(Probed: true, probedAt, probed.Terminal, probed.Host, rows, probed.Repos, Budgets.Total(agents, [.. rows.Select(static r => r.Budget)]), asOf);
    }
}
