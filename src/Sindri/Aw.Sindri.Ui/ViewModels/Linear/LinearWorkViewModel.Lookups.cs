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

    // The lookups of each team in the rows, once per team: states, active members, labels and cycles. They are not per-read data, so an answer
    // is used even when a newer list read has started; a team whose lookups failed is asked again by the next read.
    private async Task Lookups(Uri url, CancellationToken cancellationToken)
    {
        var teams = Filter("team").Choices.Select(c => c.Value).OfType<string>().Where(t => !_lookedUp.Contains(t) && _asking.Add(t)).ToList();
        foreach (var team in teams)
        {
            try
            {
                var answer = await _api.LinearLookups(url, team, cancellationToken).ConfigureAwait(true);
                if (answer.IsSuccess)
                {
                    _ = _lookedUp.Add(team);
                    OfferLookups(answer.Value);
                }
            }
            finally
            {
                _ = _asking.Remove(team);
            }
        }
    }

    private void OfferLookups(LinearLookups lookups)
    {
        foreach (var state in lookups.States)
        {
            Filter("state").Offer(state.Id, state.Name, state.Position);
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
