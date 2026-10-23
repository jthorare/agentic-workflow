using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Health;

/// <summary>Folds the status inputs into one <see cref="HealthReport"/> (UI spec E-14; P2 plan G10): stalled &gt; rebuilding &gt; degraded &gt; healthy.</summary>
public static class HealthSummary
{
    private const string SchedulerCode = "SND-SCHED-001";

    /// <summary>The health for these inputs.</summary>
    /// <param name="running">Whether this process holds the lease.</param>
    /// <param name="views">The projector's status.</param>
    /// <param name="services">The services view's rows, or null when the views can't be read.</param>
    /// <param name="profile">Where the profile stands against its approval.</param>
    /// <param name="codeIndex">The code index's problems, one per repo at most; they follow the services' and come before the profile's.</param>
    /// <returns>The health.</returns>
    public static HealthReport For(bool running, ViewsStatus views, IReadOnlyList<ServiceRow>? services, ProfileApproval profile, IReadOnlyList<HealthProblem>? codeIndex = null)
    {
        ArgumentNullException.ThrowIfNull(views);
        List<HealthProblem> problems = [];
        if (!running)
        {
            problems.Add(new("SND-API-001", "Sindri isn't running: run `sindri`", "Sindri isn't running"));
        }

        problems.AddRange(Views(views));
        if (services is null && !views.Stalled)
        {
            problems.Add(new("SND-VIEW-001", EmptyText.Unreadable(), "the views can't be read"));
        }

        problems.AddRange((services ?? []).Where(s => s.LastRun is { Outcome: ServiceOutcome.Failed }).Select(s => Failed(s.Name, s.LastRun)));
        problems.AddRange(codeIndex ?? []);
        problems.AddRange(Profile(profile));
        var word = views.Stalled ? HealthWord.Stalled : views.Rebuilding ? HealthWord.Rebuilding : (problems.Count > 0 ? HealthWord.Degraded : HealthWord.Healthy);
        return new HealthReport(word, [.. problems], Sentence(word, problems));
    }

    private static IEnumerable<HealthProblem> Profile(ProfileApproval profile)
    {
        if (profile == ProfileApproval.Changed)
        {
            yield return new("SND-PROFILE-003", "The live profile changed since approval: run `sindri profile approve`", "the profile changed since approval");
        }
        else if (profile == ProfileApproval.NeedsApproval)
        {
            yield return new("SND-PROFILE-005", "Not approved: run `sindri profile approve`", "the profile isn't approved");
        }
    }

    private static IEnumerable<HealthProblem> Views(ViewsStatus views)
    {
        if (views.Stalled)
        {
            yield return views.StalledAt is { } at
                ? new("SND-VIEW-001", EmptyText.Stalled(at), string.Create(CultureInfo.InvariantCulture, $"the views stopped at event {at}"))
                : new("SND-VIEW-001", EmptyText.Failing(views.StalledType), "the views can't be updated");
        }
        else if (views.Rebuilding)
        {
            yield return new("SND-API-010", EmptyText.Rebuilding(views.Cursor, views.LedgerHead),
                string.Create(CultureInfo.InvariantCulture, $"the views are replaying (cursor {views.Cursor} of {views.LedgerHead})"));
        }
    }

    private static HealthProblem Failed(ServiceName name, LastRun? run)
    {
        var slot = run?.Slot.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return new(run?.Code ?? SchedulerCode, $"Service {name.Value} failed at {slot}", $"service {name.Value} failed");
    }

    private static string? Sentence(HealthWord word, List<HealthProblem> problems)
    {
        if (problems.Count == 0)
        {
            return null;
        }

        var clauses = problems.ConvertAll(p => p.Clause);
        var joined = clauses.Count switch
        {
            1 => clauses[0],
            2 => $"{clauses[0]} and {clauses[1]}",
            _ => $"{string.Join(", ", clauses.Take(clauses.Count - 1))}, and {clauses[^1]}",
        };
        return $"Sindri is {word.ToString().ToLowerInvariant()}: {joined}.";
    }
}
