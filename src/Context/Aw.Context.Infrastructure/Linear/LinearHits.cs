using System.Globalization;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Maps Linear's records to search hits: the canonical key, the updatedAt version, the title as '[ENG-482] Title', and the identifier alias.</summary>
/// <param name="scope">The profile's teams (an alias names only one of them).</param>
/// <param name="now">When Linear answered.</param>
/// <param name="asOf">The query's as-of time, or null.</param>
internal sealed class LinearHits(LinearScope scope, DateTimeOffset now, DateTimeOffset? asOf)
{
    private static readonly SourceId _id = SourceId.From("linear");

    /// <summary>The version of a record: its updatedAt as an ISO instant.</summary>
    /// <param name="updatedAt">When Linear says the record last changed.</param>
    /// <returns>The version text.</returns>
    public static string Version(DateTimeOffset updatedAt) => updatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>An issue's title ('[ENG-482] Title').</summary>
    /// <param name="issue">The issue.</param>
    /// <returns>The title.</returns>
    public static string Title(LinearIssue issue) => $"[{issue.Identifier}] {issue.Title}";

    public SourceHit? Of(LinearIssue issue) =>
        Hit(LinearRecordKeys.Issue(Id(issue.Id)), new Facts(issue.Id, Title(issue), issue.UpdatedAt, issue.Url, Alias(issue)));

    public SourceHit? Of(LinearDocument document) => Hit(LinearRecordKeys.Document(Id(document.Id)), new Facts(document.Id, document.Title, document.UpdatedAt, document.Url, Alias: null));

    public SourceHit? Of(LinearProject project) => Hit(LinearRecordKeys.Project(Id(project.Id)), new Facts(project.Id, project.Name, project.UpdatedAt, project.Url, Alias: null));

    public SourceHit? Of(LinearProjectUpdate update) =>
        Hit(LinearRecordKeys.Update(Id(update.Id)), new Facts(update.Id, $"Project update: {update.Project?.Name ?? "project"} ({LinearText.Day(update.CreatedAt)})", update.UpdatedAt, update.Url, Alias: null));

    private sealed record Facts(string Id, string Title, DateTimeOffset UpdatedAt, string? Url, string? Alias);

    private static ExternalId Id(string id) => ExternalId.TryFrom(id, out var external) ? external : ExternalId.From("invalid");

    private SourceHit? Hit(RecordKey? key, Facts facts)
    {
        if (key is not { } canonical || !ExternalId.TryFrom(facts.Id, out var external))
        {
            return null;
        }

        var permalink = Uri.TryCreate(facts.Url, UriKind.Absolute, out var link) && link.Scheme == Uri.UriSchemeHttps ? link : null;
        var leaky = asOf is { } cut && facts.UpdatedAt > cut;
        return new SourceHit(new SourceRef(_id, external, SourceVersion.From(Version(facts.UpdatedAt)), permalink, canonical), facts.Title, Rank: 0, now, leaky)
        {
            Aliases = facts.Alias is null ? [] : [facts.Alias],
        };
    }

    // The identifier is an alias when it is Linear's own for this issue and its team is one of the profile's.
    private string? Alias(LinearIssue issue) =>
        scope.IsScoped && issue.Team is { } team && scope.Reads(team.Key)
        && issue.Identifier.StartsWith(team.Key + "-", StringComparison.Ordinal) && issue.Identifier.Length > team.Key.Length + 1
        && issue.Identifier[(team.Key.Length + 1)..].All(char.IsAsciiDigit)
            ? issue.Identifier
            : null;
}
