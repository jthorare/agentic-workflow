using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>The conflict half of <see cref="GitHubPullStates"/>: the stored conflict state and the step it takes from a pull request's detail.</summary>
internal sealed partial class GitHubPullStates
{
    // The next conflict state and event from the pull request's detail.
    private static Step ConflictStep(Walk walk, JsonElement pull, string number, JsonElement detail)
    {
        var state = PollJson.Text(PollJson.Member(detail, "mergeable_state"));
        if (state is null || string.Equals(state, "unknown", StringComparison.Ordinal))
        {
            // why: a pull request opened since the last poll and seen while GitHub works it out gets an entry, or its first dirty would read as a baseline;
            // an old one gets none, so its first known state is the baseline (a conflict that predates the connection fires nothing)
            return new Step(walk.Before(number) is null && walk.IsNew(pull) ? Unknown + ":0" : null, Event: null, Limited: null);
        }

        // why: the state does not hold the head, so a push that leaves the pull request dirty fires nothing; the count makes the next dirty spell a new event
        var (was, count) = ConflictState(walk.Before(number));
        if (!string.Equals(state, "dirty", StringComparison.Ordinal))
        {
            return new Step("clean:" + Count(count), Event: null, Limited: null);
        }

        var spell = string.Equals(was, "dirty", StringComparison.Ordinal) ? count : count + 1;
        var turned = spell == count ? null : GitHubEventMapper.Conflict(detail, walk.Repo, UpdatedAt(walk, detail), spell);
        return new Step("dirty:" + Count(spell), turned, Limited: null);
    }

    private static string Count(int count) => count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // The stored conflict state: 'dirty:2' / 'clean:2' (2 = times it turned dirty), 'unknown:0' (a new pull request first seen before GitHub worked it out, read as not dirty);
    // an older cursor's 'sha:dirty' reads as dirty with 0.
    private static (string State, int Count) ConflictState(string? stored)
    {
        if (stored is null)
        {
            return (string.Empty, 0);
        }

        var parts = stored.Split(':');
        return parts is ["dirty" or "clean" or Unknown, var n] && int.TryParse(n, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count) ? (parts[0], count) : (parts[^1], 0);
    }
}
