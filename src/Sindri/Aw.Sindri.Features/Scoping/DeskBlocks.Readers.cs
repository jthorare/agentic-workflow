using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The desk blocks over the context engine's JSON and the brief file (SC-K2): read-brief, anchor, reanchor, gather and propose. The person hands the
/// desk what the Step would have fetched: the brief's text (briefSource, read by the CLI under the path guard), the text shape's answer (contextQuery),
/// the views of 'context lenses|code|history' (lensesView, codeView, historyView) and the answer of 'context map propose' (proposeResult).
/// </summary>
internal static partial class DeskBlocks
{
    private static Result<ScopingDeskResponse> Needs(string block, ScopeFacts facts, string hint, params string[] keys) =>
        SindriErrors.InvalidInput.With($"{block} needs {string.Join(", ", facts.Missing(keys))} {hint}");

    private static Result<ScopingDeskResponse> ReadBrief(ScopeFacts facts)
    {
        if (facts.Missing("scopeInput", "briefSource", "contextQuery") is { Count: > 0 })
        {
            return Needs("read-brief", facts, "(fix: pass --repo-root for the file the scopeInput names, and write the answer of `sindri context query --json` over the brief's text as contextQuery.json)", "scopeInput", "briefSource", "contextQuery");
        }

        if (!string.Equals(ScopeJson.Str(facts["scopeInput"], "kind"), "brief", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"read-brief reads a brief; fact:scopeInput has the kind '{ScopeJson.Str(facts["scopeInput"], "kind") ?? "none"}' (fix: use the reader for that kind)");
        }

        var (path, section, text, body) = BriefText(facts);
        if (string.IsNullOrWhiteSpace(body))
        {
            return Reply("step.scope.read-brief", "empty", Outputs(), []);
        }

        var anchors = Suggested(facts["contextQuery"]);
        if (anchors.Error is { } error)
        {
            return error;
        }

        var source = facts.Require("briefSource");
        var reference = new JsonObject
        {
            ["id"] = "R1",
            ["source"] = "brief",
            ["key"] = section is null ? path : $"{path}#{section}",
            ["version"] = ScopeJson.Str(source, "version") ?? ("sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12]),
            ["trust"] = "untrusted",
            ["fetchedAt"] = ScopeJson.Str(source, "fetchedAt") ?? string.Empty,
        };
        var brief = new JsonObject { ["text"] = body, ["refs"] = new JsonArray(reference), ["subject"] = Subject(body, path) };
        return WithWarnings("step.scope.read-brief", "read", Outputs(("brief", brief), ("anchors", ScopeJson.ToArray(anchors.Anchors))), anchors.Warnings);
    }

    /// <summary>The part of a brief file the Step reads: the whole text, or the scope input's numbered section of it (empty when the file has no such heading).</summary>
    /// <param name="scopeInput">The scope input.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>The body.</returns>
    internal static string BriefBody(JsonNode? scopeInput, string text) =>
        ScopeJson.Str(scopeInput, "section") is { } section ? Section(text, section) : text;

    private static (string Path, string? Section, string Text, string Body) BriefText(ScopeFacts facts)
    {
        var source = facts.Require("briefSource");
        var path = ScopeJson.Str(source, "path") ?? ScopeJson.Str(facts["scopeInput"], "path") ?? "brief.md";
        var section = ScopeJson.Str(facts["scopeInput"], "section");
        var text = ScopeJson.Str(source, "text") ?? string.Empty;
        return (path, section, text, section is null ? text : Section(text, section));
    }

    private static Result<ScopingDeskResponse> Anchor(ScopeFacts facts)
    {
        if (facts.Missing("brief", "contextQuery") is { Count: > 0 })
        {
            return Needs("anchor", facts, "(fix: write the brief and the answer of `sindri context query --json` over its text as brief.json and contextQuery.json)", "brief", "contextQuery");
        }

        var anchors = Suggested(facts["contextQuery"]);
        return anchors.Error is { } error
            ? error
            : WithWarnings("step.scope.anchor", "anchored", Outputs(("anchors", ScopeJson.ToArray(anchors.Anchors))), anchors.Warnings);
    }

    private static Result<ScopingDeskResponse> Reanchor(ScopeFacts facts)
    {
        if (facts.Missing("scopeMap", "anchors") is { Count: > 0 })
        {
            return Needs("reanchor", facts, "(fix: write the changed scope map and the current anchors as scopeMap.json and anchors.json)", "scopeMap", "anchors");
        }

        var keys = ScopeJson.Items(facts["scopeMap"], "surfaces").Select(static s => ScopeJson.Str(s, "key")).OfType<string>().ToList();
        var suggested = Suggested(facts["contextQuery"], required: false);
        var ordered = ScopeJson.Strings(facts["anchors"]).Where(static a => a.StartsWith("code:", StringComparison.Ordinal))
            .Concat(ScopeJson.Strings(facts["gapAnchors"]))
            .Concat(keys.Where(static k => !k.StartsWith("code:", StringComparison.Ordinal)))
            .Concat(keys.Where(static k => k.StartsWith("code:", StringComparison.Ordinal)))
            .Concat(suggested.Anchors)
            .Distinct(StringComparer.Ordinal).ToList();
        return WithWarnings("step.scope.reanchor", "anchored", Outputs(("anchors", ScopeJson.ToArray(ordered))), []);
    }

    private static Result<ScopingDeskResponse> Gather(ScopeFacts facts)
    {
        if (facts.Missing("anchors", "lensesView") is { Count: > 0 })
        {
            return Needs("gather", facts, "(fix: pass --lenses <file> with the answer of `sindri context lenses <anchor>... --json`, optionally --code and --history with the code and history views, and write the anchors as anchors.json)", "anchors", "lensesView");
        }

        var assembled = LensGather.Assemble(facts["lensesView"], facts["codeView"], facts["historyView"], [.. ScopeJson.Strings(facts["anchors"])]);
        if (LensShape.GatherFault(assembled.Lenses, assembled.Coverage) is { } fault)
        {
            return SindriErrors.InvalidInput.With(fault);   // why: the lenses are deterministic code over the context engine's views, so gathering again serves the same fault
        }

        return Reply("step.scope.gather", "gathered", Outputs(("lenses", assembled.Lenses), ("coverage", assembled.Coverage)), []);
    }

    private static Result<ScopingDeskResponse> Propose(ScopeFacts facts)
    {
        if (facts.Missing("scopeMap") is { Count: > 0 })
        {
            return Needs("propose", facts, "(fix: write the scope map as scopeMap.json)", "scopeMap");
        }

        var map = facts.ObjectCopy("scopeMap");
        var answered = ScopeJson.Items(facts["proposeResult"], "surfaces").ToList();
        foreach (var surface in ScopeJson.Items(map, "surfaces").OfType<JsonObject>())
        {
            if (ScopeJson.Str(surface, "key") is not null)
            {
                surface["graphStatus"] = ScopeJson.Str(surface, "graphStatus") ?? "mapped";
                continue;
            }

            var answer = answered.FirstOrDefault(a => string.Equals(ScopeJson.Str(a, "surface") ?? ScopeJson.Str(a, "label"), ScopeJson.Str(surface, "title"), StringComparison.Ordinal));
            surface["graphStatus"] = Status(answer);
            if (ScopeJson.Str(answer, "node") is { } node)
            {
                surface["key"] = node;
            }
        }

        var proposed = facts.Has("proposeResult");
        return Reply("step.scope.propose", proposed ? "proposed" : "skipped", Outputs(("scopeMap", map), ("mapFixes", FixesWithStatus(facts, proposed ? "recorded" : "skipped"))), []);
    }

    // The relevance review's map fixes, carried through with the status of what became of them: recorded, none are submitted to the context engine yet. No fixes fact is an empty list.
    private static JsonObject FixesWithStatus(ScopeFacts facts, string status)
    {
        var fixes = facts.ObjectCopy("mapFixes");
        if (fixes["fixes"] is not JsonArray list)
        {
            fixes["fixes"] = new JsonArray();
            return fixes;
        }

        foreach (var fix in list.OfType<JsonObject>())
        {
            fix["status"] = status;
        }

        return fixes;
    }

    private static string Status(JsonNode? answer)
    {
        var status = ScopeJson.Str(answer, "status");
        return status is not null && new[] { "mapped", "new", "unmapped" }.Contains(status, StringComparer.Ordinal) ? status : "unknown";
    }

    private static Result<ScopingDeskResponse> WithWarnings(string block, string outcome, IReadOnlyDictionary<string, string> outputs, IReadOnlyList<ScopeFinding> warnings) =>
        new ScopingDeskResponse(block, outcome, outputs, [], [], warnings);

    /// <summary>The text shape's suggested anchors, all of them, and a warning when it had none.</summary>
    private static (List<string> Anchors, List<ScopeFinding> Warnings, Error? Error) Suggested(JsonNode? query, bool required = true)
    {
        if (!required && query is null)
        {
            return ([], [], null);
        }

        if (ScopeJson.Arr(query, "suggestedAnchors") is not { } listed)
        {
            return ([], [], SindriErrors.InvalidInput.With("contextQuery has no suggestedAnchors (fix: write the answer of `sindri context query --json` over the brief's text; it is the text shape)"));
        }

        var all = ScopeJson.Strings(listed).Distinct(StringComparer.Ordinal).ToList();
        var warnings = new List<ScopeFinding>();
        if (all.Count == 0)
        {
            warnings.Add(new ScopeFinding("DESK-ANCHORS", Item: null, "the text shape suggested no anchors; gather has nothing to search from", "Add anchors to anchors.json by hand before gather.", ScopeSeverity.Warn));
        }

        return (all, warnings, null);
    }

    /// <summary>The '## N.' heading's block: from the heading to the next '## ' heading or the end; empty when the file has no such heading.</summary>
    private static string Section(string text, string number)
    {
        var heading = new Regex($@"^##[ \t]+{Regex.Escape(number)}(\.|[ \t]|$)", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (heading.Match(text) is not { Success: true } start)
        {
            return string.Empty;
        }

        var next = new Regex(@"^##[ \t]", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Match(text, start.Index + start.Length);
        return (next.Success ? text[start.Index..next.Index] : text[start.Index..]).TrimEnd() + "\n";
    }

    private static string Subject(string body, string path)
    {
        var title = body.Split('\n').Select(static l => l.TrimEnd()).FirstOrDefault(static l => l.StartsWith("# ", StringComparison.Ordinal));
        return title is null ? Path.GetFileNameWithoutExtension(path) : title[2..].Trim();
    }
}
