using System.Globalization;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>The plain words the whole map's "no map yet" answer adds to its message: how far a map build is and what is set up. Only what the answer carries is said.</summary>
internal static class ProblemWords
{
    /// <summary>The sentence to append to the message (with its leading space), or empty when the answer carries neither member.</summary>
    internal static string Of(ApiProblem problem)
    {
        var parts = new List<string>();
        if (problem.Building is { } building)
        {
            Building(building, parts);
        }

        if (problem.NoMap is { } noMap)
        {
            NoMap(noMap, parts);
        }

        return parts.Count == 0 ? string.Empty : $" [{string.Join("; ", parts)}]";
    }

    private static void Building(ApiProblemBuilding building, List<string> parts)
    {
        if (building.StartedAt is { } started)
        {
            var job = string.IsNullOrEmpty(building.JobId) ? string.Empty : $" (job {Clean(building.JobId)})";
            parts.Add($"a map build is running since {Time(started)}{job}");
        }

        if (building is { SourcesDone: { } done, SourcesTotal: { } total })
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{done} of {total} sources read"));
        }

        if (building is { FilesDone: { } filesDone, FilesTotal: { } filesTotal })
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{filesDone} of {filesTotal} files of the current repo read"));
        }

        if (building.LastSyncAt is { } synced)
        {
            parts.Add($"last sync {Time(synced)}");
        }
    }

    private static void NoMap(ApiProblemNoMap noMap, List<string> parts)
    {
        if (noMap.Repos is { Count: > 0 } repos)
        {
            parts.Add($"repos: {string.Join(", ", repos.Select(Clean))}");
        }

        if (noMap.ApprovedAt is { } approved)
        {
            parts.Add($"approved {Time(approved)}");
        }
    }

    private static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

    private static string Clean(string text) => SafeText.Line(SafeText.Escape(text), 60);
}
