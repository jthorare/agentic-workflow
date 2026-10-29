using System.Globalization;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>
/// Builds the Projects page from the owner state in views.db and Context's facts (projects page spec §6.2 steps 3-4), pure: led minus hidden, plus attached;
/// overrides applied; ordered (§3); the page sentence (§3.1); on a Context error, attached and hand-made cards by name only.
/// </summary>
public static class ProjectCards
{
    private static readonly string[] _freshnessOrder = ["linear-projects", "linear", "github"];

    /// <summary>The finished fold keeps a project this long after it finished, unless 'all' is asked.</summary>
    public static readonly TimeSpan FinishedWindow = TimeSpan.FromDays(14);

    /// <summary>Builds the page.</summary>
    /// <param name="inputs">The owner state, Context's facts and the sources' state.</param>
    /// <param name="now">The clock's now, for the 14-day window and "N min ago".</param>
    /// <param name="all">Whether every finished project shows, not just the last 14 days.</param>
    /// <returns>The page.</returns>
    public static ProjectsPage Build(ProjectPageInputs inputs, DateTimeOffset now, bool all)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var snapshot = inputs.Facts.IsSuccess ? inputs.Facts.Value : null;
        var open = new List<ProjectCard>();
        var finished = new List<ProjectCard>();
        var hidden = new List<ProjectCard>();

        var ledCount = AddLed(inputs, snapshot, (open, finished, hidden), now, all);
        AddAttached(inputs, snapshot, (open, finished), now, all);

        return new ProjectsPage(
            EmptySentence(inputs, snapshot, ledCount),
            snapshot?.Owner?.Name,
            FreshnessRows(inputs, now),
            [.. open.OrderBy(static c => GroupRank(c.State)).ThenByDescending(static c => c.LastActivity.HasValue).ThenByDescending(static c => c.LastActivity).ThenBy(static c => c.Name, StringComparer.Ordinal)],
            [.. finished.OrderByDescending(static c => c.FinishedAt.HasValue).ThenByDescending(static c => c.FinishedAt).ThenBy(static c => c.Name, StringComparer.Ordinal)],
            [.. hidden.OrderBy(static c => c.Name, StringComparer.Ordinal)]);
    }

    private static int AddLed(ProjectPageInputs inputs, ProjectFactsSnapshot? snapshot, (List<ProjectCard> Open, List<ProjectCard> Finished, List<ProjectCard> Hidden) lists, DateTimeOffset now, bool all)
    {
        if (snapshot is null)
        {
            return 0;
        }

        var hideRows = inputs.Hidden.ToDictionary(static h => h.Linear, StringComparer.Ordinal);
        var ledCount = 0;
        foreach (var fact in snapshot.Projects.Where(static p => p.LeadIsOwner))
        {
            ledCount++;
            _ = hideRows.TryGetValue(fact.Key, out var hide);
            var card = Led(fact, hide?.Version ?? 0);
            if (hide is { Hidden: true })
            {
                lists.Hidden.Add(card);
            }
            else
            {
                Place(card, lists.Open, lists.Finished, now, all);
            }
        }

        return ledCount;
    }

    private static void AddAttached(ProjectPageInputs inputs, ProjectFactsSnapshot? snapshot, (List<ProjectCard> Open, List<ProjectCard> Finished) lists, DateTimeOffset now, bool all)
    {
        var factsByKey = snapshot is null
            ? []
            : snapshot.Projects.GroupBy(static p => p.Key, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);
        foreach (var row in inputs.Attached)
        {
            Place(AttachedCard(row, snapshot, factsByKey), lists.Open, lists.Finished, now, all);
        }
    }

    private static ProjectCard AttachedCard(ProjectAttachedRow row, ProjectFactsSnapshot? snapshot, Dictionary<string, LinearProjectFact> factsByKey)
    {
        if (row.Linear is null)
        {
            return HandMade(row);
        }

        if (snapshot is null)
        {
            return Unmatched(row, row.Linear, synced: true);
        }

        return factsByKey.TryGetValue(row.Linear, out var fact) ? Merged(row, fact) : Unmatched(row, row.Linear, synced: false);
    }

    private static void Place(ProjectCard card, List<ProjectCard> open, List<ProjectCard> finished, DateTimeOffset now, bool all)
    {
        if (card.State is not (ProjectState.Done or ProjectState.Canceled))
        {
            open.Add(card);
        }
        else if (all || card.FinishedAt is null || now - card.FinishedAt.Value <= FinishedWindow)
        {
            finished.Add(card);
        }
    }

    private static int GroupRank(ProjectState? state) => state switch
    {
        ProjectState.InProgress => 0,
        ProjectState.Planned => 1,
        ProjectState.Paused => 2,
        ProjectState.Backlog => 3,
        ProjectState.Done or ProjectState.Canceled => 4,
        null => 4,
        _ => 4,
    };

    private static ProjectCard Led(LinearProjectFact fact, long version)
    {
        var state = MapState(fact.StateRaw);
        return new ProjectCard(
            fact.Key, ProjectOrigin.Led, fact.Title, fact.Description, state, StateSetByOwner: false, MapHealth(fact.Health), new ProjectLead(You: true, Name: null),
            Date(fact.DueAt), fact.OpenIssues, fact.Counting, OpenPrs: null, PrScope: null, fact.LastActivity, LinearFinished(state, fact), Repos: [],
            LinearLinks(fact, []), Notes: null, Synced: true, version);
    }

    private static List<ProjectLink> OwnerLinks(ProjectAttachedRow row) => [.. row.Links.Select(static l => new ProjectLink(l, l))];

    private static ProjectCard HandMade(ProjectAttachedRow row) =>
        new(row.Id.Value, ProjectOrigin.HandMade, row.Name ?? string.Empty, Description: null, row.State, StateSetByOwner: row.State is not null, Health: null, Lead: null,
            row.Target, OpenIssues: null, Counting: false, OpenPrs: null, PrScope: null, row.EditedAt, row.FinishedAt, Repos: [],
            OwnerLinks(row), row.Notes, Synced: true, row.Version);

    private static ProjectCard Unmatched(ProjectAttachedRow row, string linearKey, bool synced) =>
        new(row.Id.Value, ProjectOrigin.Attached, row.Name ?? linearKey, Description: null, row.State, StateSetByOwner: row.State is not null, Health: null, Lead: null,
            row.Target, OpenIssues: null, Counting: false, OpenPrs: null, PrScope: null, LastActivity: null, row.FinishedAt, Repos: [],
            OwnerLinks(row), row.Notes, synced, row.Version);

    private static ProjectCard Merged(ProjectAttachedRow row, LinearProjectFact fact)
    {
        var linearState = MapState(fact.StateRaw);
        var byOwner = row.State is not null;
        var linearFinished = LinearFinished(linearState, fact);
        return new ProjectCard(
            row.Id.Value, ProjectOrigin.Attached, fact.Title, fact.Description, row.State ?? linearState, byOwner, MapHealth(fact.Health),
            fact.Lead is null ? null : new ProjectLead(fact.LeadIsOwner, fact.LeadIsOwner ? null : fact.Lead.Name),
            Date(fact.DueAt) ?? row.Target, fact.OpenIssues, fact.Counting, OpenPrs: null, PrScope: null, fact.LastActivity,
            byOwner ? row.FinishedAt ?? linearFinished : linearFinished ?? row.FinishedAt,
            Repos: [], LinearLinks(fact, OwnerLinks(row)), row.Notes, Synced: true, row.Version);
    }

    private static List<ProjectLink> LinearLinks(LinearProjectFact fact, List<ProjectLink> owners) =>
        fact.Url is null ? owners : [new ProjectLink("Linear", fact.Url), .. owners];

    private static DateOnly? Date(DateTimeOffset? at) => at is null ? null : DateOnly.FromDateTime(at.Value.UtcDateTime);

    private static DateTimeOffset? LinearFinished(ProjectState? state, LinearProjectFact fact) =>
        state is ProjectState.Done or ProjectState.Canceled ? fact.ClosedAt : null;

    private static ProjectState? MapState(string? raw) => raw switch
    {
        "backlog" => ProjectState.Backlog,
        "planned" or "unstarted" => ProjectState.Planned,
        "started" => ProjectState.InProgress,
        "paused" => ProjectState.Paused,
        "completed" => ProjectState.Done,
        "canceled" or "cancelled" => ProjectState.Canceled,
        _ => null,
    };

    private static ProjectHealth? MapHealth(string? raw) => raw switch
    {
        "onTrack" => ProjectHealth.OnTrack,
        "atRisk" => ProjectHealth.AtRisk,
        "offTrack" => ProjectHealth.OffTrack,
        _ => null,
    };

    private static string? EmptySentence(ProjectPageInputs inputs, ProjectFactsSnapshot? snapshot, int ledCount)
    {
        if (snapshot is null)
        {
            return "Linear and GitHub data unavailable: " + OneLine(inputs.Facts.Error.Message);
        }

        if (!inputs.LinearConfigured && inputs.Attached.Count == 0)
        {
            return "No projects yet. Connect Linear or add one by hand.";
        }

        if (!snapshot.FirstReadDone)
        {
            return "Linear is still syncing. Projects you lead show up when it's done.";
        }

        if (ledCount == 0 && inputs.Attached.Count == 0)
        {
            return snapshot.Owner?.Name is { Length: > 0 } name
                ? $"You don't lead any Linear projects as {name}. Attach one you drive, or add one by hand."
                : "You don't lead any Linear projects. Attach one you drive, or add one by hand.";
        }

        return null;
    }

    private static List<ProjectFreshnessRow> FreshnessRows(ProjectPageInputs inputs, DateTimeOffset now)
    {
        var rows = new List<ProjectFreshnessRow>();
        if (inputs.Freshness.IsSuccess)
        {
            var sources = inputs.Freshness.Value;
            foreach (var source in sources.OrderBy(static s => OrderOf(s.Source)))
            {
                rows.Add(source.LastCode is not null
                    ? new ProjectFreshnessRow(source.Source, ProjectFreshnessState.Failing, $"needs attention ({source.LastCode})", source.LastSync)
                    : !source.Usable
                        ? new ProjectFreshnessRow(source.Source, ProjectFreshnessState.Syncing, "still syncing", source.LastSync)
                        : new ProjectFreshnessRow(source.Source, ProjectFreshnessState.Ok, SyncedText(source.LastSync, now), source.LastSync));
            }
        }

        if (inputs.Facts.IsError || inputs.Freshness.IsError)
        {
            var error = inputs.Facts.IsError ? inputs.Facts.Error : inputs.Freshness.Error;
            rows.Add(new ProjectFreshnessRow("context", ProjectFreshnessState.Failing, OneLine(error.Message), At: null));
        }

        return rows;
    }

    private static int OrderOf(string source)
    {
        var index = Array.IndexOf(_freshnessOrder, source);
        return index < 0 ? _freshnessOrder.Length : index;
    }

    private static string SyncedText(DateTimeOffset? at, DateTimeOffset now)
    {
        if (at is null)
        {
            return "synced";
        }

        var minutes = (long)Math.Floor((now - at.Value).TotalMinutes);
        return minutes switch
        {
            < 1 => "synced just now",
            < 60 => string.Create(CultureInfo.InvariantCulture, $"synced {minutes} min ago"),
            _ => string.Create(CultureInfo.InvariantCulture, $"synced {minutes / 60} h ago"),
        };
    }

    private static string OneLine(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return line;
    }
}
