using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The filter bar's choices (<see cref="LinearWorkViewModel"/>, continued): what the rows carry, and the lookups of each team the rows name. Choices only grow, so a pick never vanishes.</summary>
internal sealed partial class LinearWorkViewModel
{
    // The choices the rows themselves offer: lookups have no projects, and a session cannot read lookups at all.
    private void OfferFrom(LinearIssueRow row)
    {
        Filter("team").Offer(row.Team.Key, row.Team.Key);
        Filter("state").Offer(row.State.Id, row.State.Name);
        if (row.Project is { } project)
        {
            Filter("project").Offer(project.Id, project.Name);
        }

        if (row.Cycle is { } cycle)
        {
            Filter("cycle").Offer(cycle.Id, cycle.Name);
        }

        if (row.Assignee is { } assignee)
        {
            Filter("assignee").Offer(assignee.Id, assignee.Name);
        }

        foreach (var label in row.Labels)
        {
            Filter("label").Offer(label.Id, label.Name);
        }
    }

    // The lookups of each team in the rows, once per team: states, active members, labels and cycles.
    private async Task Lookups(Uri url, int read, CancellationToken cancellationToken)
    {
        var teams = Filter("team").Choices.Select(c => c.Value).OfType<string>().Where(t => !_lookedUp.Contains(t)).ToList();
        foreach (var team in teams)
        {
            _ = _lookedUp.Add(team);   // a team whose lookups failed is not asked again on every tick
            var answer = await _api.LinearLookups(url, team, cancellationToken).ConfigureAwait(true);
            if (read != _reads)
            {
                return;
            }

            if (answer.IsSuccess)
            {
                OfferLookups(answer.Value);
            }
        }
    }

    private void OfferLookups(LinearLookups lookups)
    {
        foreach (var state in lookups.States.OrderBy(s => s.Position))
        {
            Filter("state").Offer(state.Id, state.Name);
        }

        foreach (var member in lookups.Members.Where(m => m.Active))
        {
            Filter("assignee").Offer(member.Id, member.Name);
        }

        foreach (var label in lookups.Labels)
        {
            Filter("label").Offer(label.Id, label.Name);
        }

        foreach (var cycle in lookups.Cycles)
        {
            Filter("cycle").Offer(cycle.Id, cycle.Name);
        }
    }
}
