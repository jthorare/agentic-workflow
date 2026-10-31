using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Pages;

/// <summary>A map with two suggested pages (a route with two actions, a screen with one), a route in a '.ts' file that is no page, and two concepts; and the scopes the pages tests read it under.</summary>
internal static class PagesWorld
{
    internal const string Week = "surface:route/GET:/schedule/week";
    internal const string Settings = "surface:screen/SettingsScreen";

    internal static string Yaml() => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"concept:self-scheduling\", label: \"Self-scheduling\", attributes: {summary: \"People pick shifts.\", source: derived}}",
        "  - {key: \"concept:approval-workflow\", label: \"Approval workflow\", attributes: {summary: \"Managers approve.\", source: derived}}",
        "  - {key: \"concept:billing\", label: \"Billing\", attributes: {summary: \"Invoices.\", source: derived}}",
        $"  - {{key: \"{Week}\", label: \"GET /schedule/week\", provenance: deterministic, evidence: [{{key: \"repo:aw-split-panes/src/Week.tsx\"}}]}}",
        "  - {key: \"action:WeekPage.submit\", label: \"submit\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/Week.tsx\"}]}",
        "  - {key: \"action:WeekPage.cancel\", label: \"cancel\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/Week.tsx\"}]}",
        $"  - {{key: \"{Settings}\", label: \"SettingsScreen\", provenance: deterministic, evidence: [{{key: \"repo:aw-split-panes/src/Settings.tsx\"}}]}}",
        "  - {key: \"action:SettingsScreen.save\", label: \"save\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/Settings.tsx\"}]}",
        "  - {key: \"surface:route/GET:/api/items\", label: \"GET /api/items\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/api.ts\"}]}",
        "  - {key: \"code:src/Week.tsx\", label: \"Week.tsx\", provenance: deterministic}",
        "  - {key: \"code:src/Settings.tsx\", label: \"Settings.tsx\", provenance: deterministic}",
        "  - {key: \"code:src/api.ts\", label: \"api.ts\", provenance: deterministic}",
        "edges:",
        $"  - {{from: \"code:src/Week.tsx\", to: \"{Week}\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:aw-split-panes/src/Week.tsx\"}}]}}",
        $"  - {{from: \"code:src/Settings.tsx\", to: \"{Settings}\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:aw-split-panes/src/Settings.tsx\"}}]}}",
        "  - {from: \"code:src/api.ts\", to: \"surface:route/GET:/api/items\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/api.ts\"}]}",
        "  - {from: \"code:src/Week.tsx\", to: \"action:WeekPage.submit\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/Week.tsx\"}]}",
        $"  - {{from: \"action:WeekPage.submit\", to: \"{Week}\", type: on, provenance: deterministic}}",
        $"  - {{from: \"action:WeekPage.cancel\", to: \"{Week}\", type: on, provenance: deterministic}}",
        $"  - {{from: \"action:SettingsScreen.save\", to: \"{Settings}\", type: on, provenance: deterministic}}",
        "  - {from: \"action:WeekPage.submit\", to: \"concept:self-scheduling\", type: expresses, provenance: deterministic}",
        $"  - {{from: \"{Settings}\", to: \"concept:approval-workflow\", type: expresses, provenance: deterministic}}");

    internal static ContextRepo Repo(params PagePin[] pages) => new(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { Pages = pages };

    internal static ContextScope Human(params PagePin[] pages) => MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [Repo(pages)]);

    internal static PagePin Named(string id, string name, string surface, params PageActionPin[] actions) =>
        new(PageId.From(id), name, ContextRepoName.From("aw-split-panes"), surface, actions);

    internal static PagesQuery Query(PageMarks marks = PageMarks.All, string? page = null, string? concept = null, string? repo = null, int? limit = null, int? offset = null) =>
        new(concept is null ? null : ConceptId.From(concept), page, repo is null ? null : ContextRepoName.From(repo), marks, limit, offset, Draft: false, MapVersion: null);
}
