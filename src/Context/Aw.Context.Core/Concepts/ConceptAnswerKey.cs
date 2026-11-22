using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// The key a stored concepts or glossary answer is kept under: a sha256 of everything the answer reads, so a changed input misses and builds a fresh answer, never a stale one.
/// That is the map version (and the label it was asked by, so a version named with another commit is still refused), the override and intent layers, the profile's context
/// section and every field of its repos (deny and demote globs, roles, pages, concept pins and their aliases), the repos left out, the caller (kind, repo and client: the client
/// picks the fence policy) and the question (repo, cursor, limit). Each field is written with its length first, so two different inputs never join into one text.
/// </summary>
internal static class ConceptAnswerKey
{
    // A change to what an answer holds or how it is built bumps this, so rows built the old way are never answered again.
    private const string Format = "1";

    /// <summary>The key of one concepts page.</summary>
    internal static string Concepts(ConceptAnswerBasis basis, ConceptsQuery query, ContextScope scope)
    {
        var text = Start("concepts", basis, query.Read, scope, withIntent: true);
        Field(text, query.Repo);
        Field(text, query.Page.Cursor);
        Field(text, query.Page.Limit?.ToString(CultureInfo.InvariantCulture));
        return Hash(text);
    }

    /// <summary>
    /// The key of the glossary. It reads only the concept nodes and the pins, never a record or decision, so a human's whole-map glossary skips the intent layer and a sync doesn't
    /// miss it; a session's or a named repo's graph is cut along the layer's links, so theirs keeps it.
    /// </summary>
    internal static string Glossary(ConceptAnswerBasis basis, ConceptGlossaryQuery query, ContextScope scope)
    {
        var text = Start("glossary", basis, query.Read, scope, withIntent: scope.Caller.Kind != ContextCallerKind.Human || query.Repo is not null);
        Field(text, query.Repo);
        return Hash(text);
    }

    private static StringBuilder Start(string kind, ConceptAnswerBasis basis, MapRead read, ContextScope scope, bool withIntent)
    {
        var text = new StringBuilder();
        Field(text, Format);
        Field(text, kind);
        Field(text, read.Draft ? "draft" : "current");
        Field(text, read.Version?.Label);
        Field(text, basis.Version.ToString(CultureInfo.InvariantCulture));
        Field(text, basis.Overrides);
        Field(text, withIntent ? basis.Intent.ToString(CultureInfo.InvariantCulture) : "-");
        Field(text, scope.Settings?.GetRawText());
        Field(text, scope.OmitPages ? "omit-pages" : "pages");
        Field(text, string.Join(',', scope.LeftOut.Select(r => r.Value)));
        Field(text, scope.Caller.Kind.ToString());
        Field(text, scope.Caller.Repo?.Value);
        Field(text, scope.Caller.Client.ToString());
        Field(text, scope.Repos.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var repo in scope.Repos)
        {
            Repo(text, repo);
        }

        return text;
    }

    private static void Repo(StringBuilder text, ContextRepo repo)
    {
        Field(text, repo.Name.Value);
        Field(text, repo.Path);
        Fields(text, repo.DenyPaths);
        Fields(text, repo.DemotePaths);
        Field(text, repo.Roles.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var role in repo.Roles)
        {
            Field(text, role.Name);
            Field(text, role.Demoted ? "demoted" : "kept");
            Fields(text, role.Globs);
        }

        Field(text, MapViewCache.Pages(repo));
        Field(text, repo.Concepts.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var pin in repo.Concepts)
        {
            Field(text, pin.Id?.Value);
            Field(text, pin.Name);
            Field(text, pin.Summary);
            Fields(text, pin.Features);
            Fields(text, pin.Paths);
            Fields(text, pin.DependsOn);
            Fields(text, pin.Owns);
            Fields(text, pin.Aliases);
        }
    }

    private static void Fields(StringBuilder text, IReadOnlyList<string> values)
    {
        Field(text, values.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var value in values)
        {
            Field(text, value);
        }
    }

    // A null field is '-'; a written one is its length, ':' and the text, so 'ab'+'c' and 'a'+'bc' differ.
    private static void Field(StringBuilder text, string? value) =>
        _ = value is null ? text.Append("-;") : text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');

    private static string Hash(StringBuilder text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
}
