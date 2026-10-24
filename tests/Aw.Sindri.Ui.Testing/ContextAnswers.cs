using System.Net;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The fake's answers for the context routes: each is a golden (or a refusal golden) chosen by what was asked, never a literal typed here.</summary>
internal static class ContextAnswers
{
    private const string Lenses1 = "map-tabs";
    private const string LensesSet = "set-two-anchors";

    private static string SingleAnchor { get; } = (string)GoldenFiles.Node("context-lenses", Lenses1)["anchor"]!["key"]!;

    // The one partly-known request a golden holds, matched as sets (the resolved anchors, the missing ones), not by request order.
    private static bool HoldsMissingGolden(IEnumerable<string> resolved, IEnumerable<string> missing)
    {
        var golden = GoldenFiles.Node("context-lenses", "set-missing-anchor");
        return resolved.ToHashSet(StringComparer.Ordinal).SetEquals(golden["anchors"]!.AsArray().Select(a => (string)a!["key"]!))
            && missing.ToHashSet(StringComparer.Ordinal).SetEquals(golden["missingAnchors"]!.AsArray().Select(m => (string)m!));
    }

    /// <summary>The anchors the lens goldens hold.</summary>
    internal static IReadOnlySet<string> KnownAnchors { get; } = new HashSet<string>(
        [(string)GoldenFiles.Node("context-lenses", Lenses1)["anchor"]!["key"]!, .. GoldenFiles.Node("context-lenses", LensesSet)["anchors"]!.AsArray().Select(a => (string)a!["key"]!)],
        StringComparer.Ordinal);

    /// <summary>The whole map for a query: a refusal golden for what the goldens cannot answer (an area that is not in the map, a version that is not kept), else the golden for what was asked.</summary>
    /// <param name="query">What was asked.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer WholeMap(WholeMapQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query switch
        {
            { Area: { } area } when !string.Equals(area, OpenArea, StringComparison.Ordinal) => RouteAnswer.Problem("context-whole", "area-unknown-200"),
            { Area: not null } => RouteAnswer.Api("context-whole", "area-open"),
            { MapVersion: { } version } when !string.Equals(version, "v1", StringComparison.Ordinal) => RouteAnswer.Problem("context-whole", "map-version-not-retained-120"),
            { Draft: true } => RouteAnswer.Api("context-whole", "draft"),
            { Locate.Count: > 0 } => RouteAnswer.Api("context-whole", "locate"),
            { Detail: "areas" } => RouteAnswer.Api("context-whole", "areas-over-budget"),
            _ => RouteAnswer.Api("context-whole", "nodes-small"),
        };
    }

    /// <summary>The one area the goldens hold in full.</summary>
    internal const string OpenArea = "feature:tabs.tab-groups";

    /// <summary>The lenses for a query: a refusal golden for what the goldens cannot answer, else the golden for one anchor or for a set.</summary>
    /// <param name="query">What was asked.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer Lenses(LensesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var anchors = query.Anchors.Distinct(StringComparer.Ordinal).ToList();
        if (query.Depth is > 4)
        {
            return RouteAnswer.Problem("context-lenses", "bad-depth-006");
        }

        if (query.MapVersion is { } pinned && !string.Equals(pinned, "v1", StringComparison.Ordinal))
        {
            return RouteAnswer.Problem("context-lenses", "map-version-not-retained-120");
        }

        if (query.Draft)
        {
            return RouteAnswer.Problem("context-lenses", "no-draft-044");
        }

        var known = anchors.Where(KnownAnchors.Contains).ToList();
        return anchors.Count switch
        {
            _ when known.Count == 0 => RouteAnswer.Problem("context-lenses", "unknown-anchor-003"),
            1 when !string.Equals(anchors[0], SingleAnchor, StringComparison.Ordinal) => RouteAnswer.Problem("context-lenses", "unknown-anchor-003"),   // no golden answers for this one alone
            1 when query.Depth == 1 => RouteAnswer.Api("context-lenses", "depth-1"),
            1 when string.Equals(query.MapVersion, "v1", StringComparison.Ordinal) => RouteAnswer.Api("context-lenses", "map-version-v1"),
            1 => RouteAnswer.Api("context-lenses", Lenses1),
            _ when known.Count < anchors.Count => HoldsMissingGolden(known, anchors.Except(known, StringComparer.Ordinal))
                ? RouteAnswer.Api("context-lenses", "set-missing-anchor")   // some resolve, some do not: 200 with missingAnchors, as the server answers
                : new RouteAnswer(HttpStatusCode.NotFound, "{}"),   // the real server answers 200 for this set, but no golden names its missing anchors: not served, so a test relying on it fails loudly
            _ => RouteAnswer.Api("context-lenses", LensesSet),
        };
    }

    /// <summary>The code lens for a query: the golden when every anchor is the one it holds, else the unknown-anchor refusal.</summary>
    /// <param name="query">What was asked.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer Code(CodeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var known = GoldenFiles.Node("context-code", "edges-and-lexical")["anchors"]!.AsArray().Select(a => (string)a!["key"]!).ToList();
        return query.Anchors.Count > 0 && query.Anchors.All(a => known.Contains(a, StringComparer.Ordinal))
            ? RouteAnswer.Api("context-code", "edges-and-lexical")
            : RouteAnswer.Problem("context-code", "unknown-anchor-003");
    }

    /// <summary>The file map for a query: the golden whose path matches, the unmapped one, or a refusal golden.</summary>
    /// <param name="query">What was asked.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer FileMap(FileMapQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Draft)
        {
            return RouteAnswer.Problem("context-filemap", "no-draft-044");
        }

        if (!string.Equals(query.Repo, (string)GoldenFiles.Node("context-filemap", "root")["repo"]!, StringComparison.Ordinal))
        {
            return RouteAnswer.Problem("context-filemap", "repo-outside-045");
        }

        if (query.UnmappedOnly)
        {
            return RouteAnswer.Api("context-filemap", "src-unmapped");
        }

        foreach (var scenario in (string[])["root", "src", "src-tabs"])
        {
            if (string.Equals((string)GoldenFiles.Node("context-filemap", scenario)["path"]!, query.Path ?? string.Empty, StringComparison.Ordinal))
            {
                return RouteAnswer.Api("context-filemap", scenario);
            }
        }

        return new RouteAnswer(HttpStatusCode.NotFound, "{}");
    }
}
