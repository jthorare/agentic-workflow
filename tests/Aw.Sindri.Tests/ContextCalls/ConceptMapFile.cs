namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The map the concept goldens and tests read: two concepts as the derivation will write them (attributes, citations, 'belongs-to' edges), a feature no concept takes, and code, a surface and an action under them.</summary>
internal static class ConceptMapFile
{
    /// <summary>Writes the map as YAML under <paramref name="directory"/> and answers its path; the repo is the tabs fixture's.</summary>
    /// <param name="directory">The host's home.</param>
    /// <param name="terms">Whether the concepts carry the glossary terms a build with embeddings stores (the glossary goldens).</param>
    /// <returns>The file's path.</returns>
    internal static string Write(string directory, bool terms = false)
    {
        var file = Path.Combine(directory, "concept-map.yaml");
        var scheduling = terms ? ", terms: [shift, swap, roster]" : string.Empty;   // "shift" is on both concepts: the by-term rows join them
        var billing = terms ? ", terms: [invoice, shift]" : string.Empty;
        File.WriteAllText(file, string.Join('\n',
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"concept:scheduling\", label: \"Self scheduling\", attributes: {summary: \"People pick and swap shifts.\", source: derived, seed: \"feature:shifts\"" + scheduling + "}, evidence: [{key: \"repo:aw-split-panes/docs/scheduling.md\"}]}",
            "  - {key: \"concept:billing\", label: \"Billing\", attributes: {summary: \"Invoices and payment.\", source: override" + billing + "}}",
            "  - {key: \"feature:shifts\", label: \"Shifts\"}",
            "  - {key: \"feature:swaps\", label: \"Swaps\"}",
            "  - {key: \"feature:invoices\", label: \"Invoices\"}",
            "  - {key: \"feature:lone\", label: \"Loose end\"}",
            "  - {key: \"surface:screen/shifts\", label: \"Shifts screen\"}",
            "  - {key: \"action:pick\", label: \"Pick a shift\"}",
            "  - {key: \"code:src/shifts.ts\", label: \"shifts.ts\", provenance: deterministic}",
            "  - {key: \"code:src/shared.ts\", label: \"shared.ts\", provenance: deterministic}",
            "  - {key: \"code:src/invoices.ts\", label: \"invoices.ts\", provenance: deterministic}",
            "edges:",
            "  - {from: \"feature:shifts\", to: \"concept:scheduling\", type: belongs-to}",
            "  - {from: \"feature:swaps\", to: \"concept:scheduling\", type: belongs-to}",
            "  - {from: \"feature:invoices\", to: \"concept:billing\", type: belongs-to}",
            "  - {from: \"code:src/shifts.ts\", to: \"feature:shifts\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/shifts.ts\"}]}",
            "  - {from: \"code:src/shared.ts\", to: \"feature:shifts\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/shared.ts\"}]}",
            "  - {from: \"code:src/shared.ts\", to: \"feature:swaps\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/shared.ts\"}]}",
            "  - {from: \"code:src/invoices.ts\", to: \"feature:invoices\", type: implements, provenance: deterministic, evidence: [{key: \"repo:elsewhere/src/invoices.ts\"}]}",
            "  - {from: \"feature:invoices\", to: \"feature:shifts\", type: depends-on}",
            "  - {from: \"feature:swaps\", to: \"feature:invoices\", type: depends-on}",
            "  - {from: \"feature:lone\", to: \"feature:shifts\", type: depends-on}",
            "  - {from: \"surface:screen/shifts\", to: \"feature:shifts\", type: part-of}",
            "  - {from: \"action:pick\", to: \"surface:screen/shifts\", type: on}") + "\n");
        return file;
    }
}
