using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

internal static partial class DesignPacketChecks
{
    /// <summary>DSN-COV-001: every scope surface is covered by a section, or excluded in coverage.json with a reason; coverage.json agrees with the sections.</summary>
    internal static void SurfacesCovered(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("sections.json", "coverage.json", ScopeMap))
        {
            return;
        }

        var covers = ById(Sections(facts)).ToDictionary(static p => p.Key, static p => LineJson.Strings(p.Value, "covers").ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var surfaces = ScopeSurfaces(facts).ToList();
        ScopeSurfacesAccountedFor(facts, sink, surfaces, covers);
        CoverageRowsAgree(facts, sink, covers);
        var known = surfaces.Select(static s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (section, ids) in covers)
        {
            foreach (var unknown in ids.Where(i => !known.Contains(i)))
            {
                sink.Fail(section, $"{section} covers {unknown}, which is no surface of the scope packet", "cover only the scope map's surfaces");
            }
        }
    }

    private static void ScopeSurfacesAccountedFor(LineFacts facts, LineSink sink, List<(string Id, string Key)> surfaces, Dictionary<string, HashSet<string>> covers)
    {
        var excluded = LineJson.Items(facts.Json("coverage.json"), "excluded").ToList();
        foreach (var (id, key) in surfaces)
        {
            var exclusion = excluded.Find(e => string.Equals(LineJson.Str(e, "id"), id, StringComparison.Ordinal) || string.Equals(LineJson.Str(e, "key"), key, StringComparison.Ordinal));
            if (exclusion is not null && string.IsNullOrWhiteSpace(LineJson.Str(exclusion, "reason")))
            {
                sink.Fail(id, $"{key} is excluded with no reason", "give the exclusion a one-line reason in coverage.json");
            }
            else if (exclusion is null && !covers.Values.Any(c => c.Contains(id)))
            {
                sink.Fail(id, $"{key} is covered by no section and is not excluded", "add it to a section's covers, or list it in coverage.json's excluded with a reason");
            }
        }
    }

    private static void CoverageRowsAgree(LineFacts facts, LineSink sink, Dictionary<string, HashSet<string>> covers)
    {
        foreach (var row in LineJson.Items(facts.Json("coverage.json"), "surfaces"))
        {
            var id = LineJson.Str(row, "id") ?? string.Empty;
            var listed = LineJson.Strings(row, "sections").ToList();
            if (listed.Count == 0 || listed.Exists(s => !covers.TryGetValue(s, out var set) || !set.Contains(id)))
            {
                sink.Fail(id, $"coverage.json lists sections for {id} that do not cover it", "make coverage.json and the sections' covers agree");
            }
        }
    }

    /// <summary>DSN-COV-002: every scope question is answered at the scope stage, carried as an open decision or cited by a section, or deferred with an owner and a buildMayAssume default.</summary>
    internal static void QuestionsHandled(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("scope/questions.json", "scope/answers.jsonl", "decisions.json", "sections.json", "coverage.json"))
        {
            return;
        }

        var answered = LineJson.Items(facts.Json("scope/answers.jsonl")).SelectMany(static a => LineJson.Strings(a, "questionIds")).ToHashSet(StringComparer.Ordinal);
        var carried = Decisions(facts).SelectMany(static d => LineJson.Strings(d, "cites")).Concat(Sections(facts).SelectMany(static s => LineJson.Strings(s, "cites"))).ToHashSet(StringComparer.Ordinal);
        var deferred = LineJson.Items(facts.Json("coverage.json"), "deferred").ToList();
        foreach (var question in LineJson.Items(facts.Json("scope/questions.json"), "questions"))
        {
            var id = LineJson.Str(question, "id") ?? string.Empty;
            if (!answered.Contains(id) && !carried.Contains(id))
            {
                CheckDeferred(sink, id, deferred.Find(d => string.Equals(LineJson.Str(d, "question"), id, StringComparison.Ordinal)));
            }
        }
    }

    private static void CheckDeferred(LineSink sink, string id, JsonNode? entry)
    {
        if (entry is null)
        {
            sink.Fail(id, $"scope question {id} is not answered, not carried as an open decision and not deferred", "answer it in a section, carry it as a decision citing it, or defer it in coverage.json with an owner and a buildMayAssume default");
        }
        else if (string.IsNullOrWhiteSpace(LineJson.Str(entry, "owner")) || string.IsNullOrWhiteSpace(LineJson.Str(entry, "buildMayAssume")))
        {
            sink.Fail(id, $"scope question {id} is deferred without an owner and a buildMayAssume default", "name who decides and what the build may assume meanwhile");
        }
    }

    /// <summary>DSN-CITE-001: every section cites scope ids or a decision, and each citation resolves.</summary>
    internal static void SectionsCite(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("sections.json", "acceptance.json", "decisions.json", ScopeMap, ScopeAcceptance))
        {
            return;
        }

        var citations = new Citations(facts);
        foreach (var section in Sections(facts))
        {
            var id = LineJson.Str(section, "id") ?? string.Empty;
            var cites = LineJson.Strings(section, "cites").ToList();
            if (cites.Count == 0)
            {
                sink.Fail(id, $"{id} cites no scope id and no decision", "cite the scope items and decisions the section rests on");
            }

            foreach (var cite in cites.Where(c => !citations.Resolves(c)))
            {
                sink.Fail(id, $"{id} cites {cite}, which resolves to nothing", "cite an id the scope packet or this design holds");
            }
        }
    }
}
