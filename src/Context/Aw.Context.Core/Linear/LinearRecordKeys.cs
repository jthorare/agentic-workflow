using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>The canonical keys of the records the native Linear adapter reads (spec §5.1, D16); an issue's key is the one Onyx's Linear connector writes, so the two adapters' hits merge.</summary>
public static class LinearRecordKeys
{
    private const string IssuePrefix = "linear:issue/";
    private const string DocPrefix = "linear:doc/";
    private const string ProjectPrefix = "linear:project/";
    private const string UpdatePrefix = "linear:update/";

    /// <summary>The key of a Linear issue.</summary>
    /// <param name="id">Its UUID.</param>
    /// <returns>'linear:issue/&lt;uuid&gt;', or null when the id isn't a UUID.</returns>
    public static RecordKey? Issue(ExternalId id) => Keyed(IssuePrefix, id);

    /// <summary>The key of a Linear Document.</summary>
    /// <param name="id">Its UUID.</param>
    /// <returns>'linear:doc/&lt;uuid&gt;', or null when the id isn't a UUID.</returns>
    public static RecordKey? Document(ExternalId id) => Keyed(DocPrefix, id);

    /// <summary>The key of a Linear project.</summary>
    /// <param name="id">Its UUID.</param>
    /// <returns>'linear:project/&lt;uuid&gt;', or null when the id isn't a UUID.</returns>
    public static RecordKey? Project(ExternalId id) => Keyed(ProjectPrefix, id);

    /// <summary>The key of a project update.</summary>
    /// <param name="id">Its UUID.</param>
    /// <returns>'linear:update/&lt;uuid&gt;', or null when the id isn't a UUID.</returns>
    public static RecordKey? Update(ExternalId id) => Keyed(UpdatePrefix, id);

    /// <summary>Whether a canonical key names a record of the native Linear adapter's kinds.</summary>
    /// <param name="canonical">The key.</param>
    /// <returns>True for an issue, document, project or project-update key.</returns>
    public static bool IsLinear(RecordKey canonical) =>
        canonical.Value.StartsWith(IssuePrefix, StringComparison.Ordinal) || canonical.Value.StartsWith(DocPrefix, StringComparison.Ordinal)
        || canonical.Value.StartsWith(ProjectPrefix, StringComparison.Ordinal) || canonical.Value.StartsWith(UpdatePrefix, StringComparison.Ordinal);

    /// <summary>The Linear id inside a Linear record key.</summary>
    /// <param name="canonical">The key.</param>
    /// <returns>The part after the kind prefix (the UUID), or null when the key isn't a Linear one.</returns>
    public static ExternalId? BareId(RecordKey canonical)
    {
        var key = canonical.Value;
        var prefix = new[] { IssuePrefix, DocPrefix, ProjectPrefix, UpdatePrefix }.FirstOrDefault(p => key.StartsWith(p, StringComparison.Ordinal));
        return prefix is not null && ExternalId.TryFrom(key[prefix.Length..], out var id) ? id : null;
    }

    /// <summary>The kind of the record a key from this class names.</summary>
    /// <param name="canonical">The key.</param>
    /// <returns>Issue, Doc, or Project (a project and its updates).</returns>
    public static RecordKind KindOf(RecordKey canonical)
    {
        var key = canonical.Value;
        return key.StartsWith(IssuePrefix, StringComparison.Ordinal) ? RecordKind.Issue
            : key.StartsWith(DocPrefix, StringComparison.Ordinal) ? RecordKind.Doc
            : RecordKind.Project;
    }

    private static RecordKey? Keyed(string prefix, ExternalId id) =>
        Guid.TryParseExact(id.Value, "D", out var uuid) ? RecordKey.From(prefix + uuid.ToString("D")) : null;
}
