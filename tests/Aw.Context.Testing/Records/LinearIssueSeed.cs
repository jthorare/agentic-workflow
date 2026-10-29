using System.Globalization;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Testing.Records;

/// <summary>Stored Linear issue records as the Linear feed writes them ('[KEY] title', the issue's text in chunks), for tests of what reads the store.</summary>
public static class LinearIssueSeed
{
    /// <summary>One stored Linear issue.</summary>
    /// <param name="id">The issue's UUID.</param>
    /// <param name="key">Its tracker key, 'SPL-101'.</param>
    /// <param name="title">Its title.</param>
    /// <param name="createdAt">When it was created.</param>
    /// <param name="updatedAt">When it last changed.</param>
    /// <param name="chunks">Its text, one string per chunk.</param>
    /// <returns>The write, under the 'linear' source.</returns>
    public static RecordWrite Issue(string id, string key, string title, DateTimeOffset createdAt, DateTimeOffset updatedAt, params string[] chunks)
    {
        var recordKey = LinearRecordKeys.Issue(ExternalId.From(id)) ?? throw new ArgumentException("The issue id isn't a UUID.", nameof(id));
        var record = new StoredRecord(
            recordKey, RecordSamples.Linear, RecordKind.Issue, Repo: null, Audience: AudienceKey.From("linear:team-a"), $"[{key}] {title}", Url: null, Author: null,
            RecordState.Closed, StateRaw: "Done", createdAt, updatedAt, ClosedAt: null, SourceVersion.From(updatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
            RecordSamples.Hash(string.Concat(chunks)), Ruleset: "r1", Blocked: false, RemovedAt: null, RecordSamples.Now, Links: []);
        return RecordSamples.Write(record, chunks);
    }

    /// <summary>The synthetic issues as stored records.</summary>
    /// <param name="issues">The issues, or null for the recorded split-panes fixture's.</param>
    /// <returns>One write each.</returns>
    public static IReadOnlyList<RecordWrite> From(IReadOnlyList<SyntheticIssue>? issues)
    {
        if (issues is not null)
        {
            return [.. issues.Select(i => Issue(i.Id, i.Key, i.Title, i.CreatedAt, i.UpdatedAt, [.. i.Chunks]))];
        }

        return [.. OnyxIndex.Documents.Where(d => d.SourceType == "linear").Select(Recorded)];
    }

    private static RecordWrite Recorded(OnyxDocument document)
    {
        var end = document.Title.IndexOf(']', StringComparison.Ordinal);
        var key = document.Title[1..end];
        var created = DateTimeOffset.Parse((string?)document.SearchDoc["metadata"]?["created_at"] ?? "", CultureInfo.InvariantCulture);
        return Issue(document.Id, key, document.Title[(end + 2)..], created, document.UpdatedAt, [.. document.Chunks]);
    }
}
