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
    // Bump this in the same change as anything that alters a kept answer for the same inputs: ConceptBuilder or ConceptGlossaryBuilder output, the row records or their JSON,
    // KeptConcepts' code paths, or a new input the answer reads (add that input to the key as well). Rows built the old way are then never answered again. Redaction rules need
    // no bump: their digest is part of the key.
    private const string Format = "2";   // 2: near-duplicate suggestions fold into one row (issue 2975)

    /// <summary>The key of one concepts page.</summary>
    internal static string Concepts(ConceptAnswerBasis basis, ConceptsQuery query, ContextScope scope, string ruleset)
    {
        var text = Start(new KeyHead("concepts", query.Read, WithIntent: true, ruleset), basis, scope);
        Field(text, query.Repo);
        Field(text, query.Page.Cursor);
        Field(text, query.Page.Limit?.ToString(CultureInfo.InvariantCulture));
        return Hash(text);
    }

    /// <summary>
    /// The key of the glossary. It reads only the concept nodes and the pins, never a record or decision, so a human's whole-map glossary skips the intent layer and a sync doesn't
    /// miss it; a session's or a named repo's graph is cut along the layer's links, so theirs keeps it.
    /// </summary>
    internal static string Glossary(ConceptAnswerBasis basis, ConceptGlossaryQuery query, ContextScope scope, string ruleset)
    {
        var text = Start(new KeyHead("glossary", query.Read, WithIntent: scope.Caller.Kind != ContextCallerKind.Human || query.Repo is not null, ruleset), basis, scope);
        Field(text, query.Repo);
        return Hash(text);
    }

    // The ruleset is the running redactor's digest (default rules and the profile's extra patterns): a toolkit upgrade that changes a rule misses, so no kept text skips today's scrub.
    private static StringBuilder Start(KeyHead head, ConceptAnswerBasis basis, ContextScope scope)
    {
        var (kind, read, withIntent, ruleset) = head;
        var text = new StringBuilder();
        Field(text, Format);
        Field(text, kind);
        Field(text, ruleset);
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

    // What a key starts with besides the basis and the scope: the answer's kind, the map it was asked of, whether the intent layer counts and the redactor's rules digest.
    private readonly record struct KeyHead(string Kind, MapRead Read, bool WithIntent, string Ruleset);
}
