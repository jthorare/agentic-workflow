using System.Globalization;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>A project as the project feed hands it over (projects page spec §4.1): the status type as state, the lead as a led-by link, health and target date as their own fields, and the first profile team as the audience.</summary>
internal static class LinearProjectItem
{
    private const string Prefix = "linear:project/";

    /// <summary>The item for a project.</summary>
    /// <param name="project">The project.</param>
    /// <param name="profileTeams">The profile's team keys; empty reads every team.</param>
    /// <returns>The item.</returns>
    public static RecordItem Of(LinearProjectNode project, IReadOnlyCollection<string> profileTeams)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profileTeams);
        var raw = project.Status?.Type ?? project.State;
        return new RecordItem(
            RecordKey.From(Prefix + project.Id), RecordKind.Project, project.Name, Text(project), project.Lead?.Name, project.Url, State(raw), raw,
            project.CreatedAt, project.UpdatedAt, project.CompletedAt ?? project.CanceledAt, Version(project),
            project.Lead is { } lead ? [new RecordLink("linear:user/" + lead.Id, RecordLinkKind.LedBy, lead.Name)] : [],
            Repo: null, Audience(project, profileTeams), project.LastUpdate?.Health, Due(project.TargetDate));
    }

    private static RecordState? State(string? type) => type switch
    {
        "completed" => RecordState.Done,
        "canceled" => RecordState.Canceled,
        "backlog" or "planned" or "started" or "paused" => RecordState.Open,
        _ => null,
    };

    // why: a project update in Linear may not bump the project's updatedAt, so the newest update's time is part of the version
    private static SourceVersion Version(LinearProjectNode project) =>
        SourceVersion.From(Iso(project.UpdatedAt) + "|" + (project.LastUpdate?.CreatedAt is { } at ? Iso(at) : string.Empty));

    private static string Text(LinearProjectNode project) => string.Join("\n\n", new[]
    {
        project.Name,
        project.Description,
        project.Status?.Name is { Length: > 0 } status ? "Status: " + status : null,
        project.TargetDate is { Length: > 0 } target ? "Target: " + target : null,
        project.LastUpdate?.Health is { Length: > 0 } health ? "Health: " + health : null,
    }.Where(static s => !string.IsNullOrWhiteSpace(s)));

    private static AudienceKey? Audience(LinearProjectNode project, IReadOnlyCollection<string> profileTeams)
    {
        var keys = (project.Teams?.Nodes ?? []).Select(static t => t.Key).Order(StringComparer.Ordinal).ToList();
        var key = profileTeams.Count > 0 ? keys.FirstOrDefault(profileTeams.Contains) : keys.FirstOrDefault();
        return key is not null && AudienceKey.TryFrom("linear:" + key, out var audience) ? audience : null;
    }

    private static DateOnly? Due(string? target) =>
        DateOnly.TryParseExact(target, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static string Iso(DateTimeOffset at) => at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
