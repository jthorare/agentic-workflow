using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The 'runs' fact an engine-run scoping instance never stores (#1008): one record per second-opinion-relevant Step, built from the instance's 'launch.started' rows, so SCP-CHAL-002
/// can compare the agents that really ran. A record is {step, via: "agent", target}, 'target' being the launcher agent id. The Steps are the recipe's draft and challenge states.
/// </summary>
internal static class ScopingRuns
{
    private static readonly (string State, string Step)[] _steps = [("draft", "step.scope.draft"), ("challenge", "step.scope.challenge")];

    /// <summary>The runs array as JSON text.</summary>
    /// <param name="launches">The launches view.</param>
    /// <param name="address">The instance's address, whose first part is its item; null when there is none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The JSON; null when the instance has not launched both the draft and the challenge on an agent, so the rule keeps warning that no run record is kept.</returns>
    internal static async ValueTask<Result<string?>> Of(ILaunchViews launches, string? address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launches);
        if (address is null || !ItemId.TryFrom(address.Split('/')[0], out var item))
        {
            return (string?)null;
        }

        var rows = await launches.ForItem(item, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var runs = new JsonArray();
        foreach (var (state, step) in _steps)
        {
            // why: the view lists the newest request first, so the first started row of the state is its latest run
            var row = rows.Value.FirstOrDefault(r => r.State == LaunchState.Started && r.Step.Instance is { } instance && string.Equals(instance.Value, address, StringComparison.Ordinal) && string.Equals(r.Step.State, state, StringComparison.Ordinal));
            if (row is null)
            {
                return (string?)null;
            }

            JsonNode record = new JsonObject { ["step"] = step, ["via"] = "agent", ["target"] = row.Target.Value };   // why: typed as JsonNode so Add(JsonNode) binds, not the generic Add<T> that isn't AOT-safe
            runs.Add(record);
        }

        return runs.ToJsonString();
    }
}
