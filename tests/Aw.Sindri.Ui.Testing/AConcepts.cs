using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>Builders for the concepts answer: a concept with sensible defaults, the states the server serves, and the answer as the route serves it.</summary>
internal static class AConcepts
{
    /// <summary>One concept.</summary>
    internal static ContextConcept Concept(string id, string name, string summary = "", int features = 1, int records = 0, int decisions = 0, int code = 0, string source = "derived") =>
        new(id, name, summary, new ConceptSize(features, records, decisions, code), source, []);

    /// <summary>The synthetic concept of the unassigned nodes.</summary>
    internal static ConceptOther Other(int features = 0, int records = 0, int decisions = 0, int code = 0) => new("concept:other", "Other", new ConceptSize(features, records, decisions, code));

    /// <summary>An ok answer.</summary>
    internal static ContextConceptsView Ok(IReadOnlyList<ContextConcept> concepts, ConceptOther? other = null, IReadOnlyList<ConceptLink>? links = null, string? next = null) =>
        new(1, "aw-split-panes", "ok", "v1", concepts, other ?? Other(), links, next);

    /// <summary>The three concepts most tests use: Self scheduling, Billing and Sync, linked Self scheduling to Billing, and a little unassigned.</summary>
    internal static ContextConceptsView Three() => Ok(
        [
            Concept("concept:self-scheduling", "Self scheduling", "People book their own slots.", features: 4, records: 3, decisions: 2, code: 40),
            Concept("concept:billing", "Billing", "Invoices and payments.", features: 2, records: 1, decisions: 1, code: 12),
            Concept("concept:sync", "Sync", "Calendars stay in step.", features: 1, records: 0, decisions: 0, code: 7),
        ],
        Other(features: 1, code: 3),
        [new ConceptLink("concept:self-scheduling", "concept:billing", 1)]);

    /// <summary>A state with no concepts to list: empty, building or unavailable.</summary>
    internal static ContextConceptsView Without(string state, string? code = null) => new(1, "aw-split-panes", state, Code: code);

    /// <summary>A glossary answer: the state and each concept's terms.</summary>
    internal static ConceptGlossaryView Glossary(string state, params ConceptTerms[] concepts) => new(1, "aw-split-panes", "v1", state, concepts, []);

    /// <summary>The answer as the route serves it.</summary>
    internal static RouteAnswer Served(ContextConceptsView view) => new(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(view, SindriApiClient.Json)));

    /// <summary>The glossary answer as the route serves it.</summary>
    internal static RouteAnswer Served(ConceptGlossaryView view) => new(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(view, SindriApiClient.Json)));

    /// <summary>A pin-suggest answer for a concept: its terms and the new records each would link.</summary>
    internal static PinSuggestView Suggested(params PinSuggestTerm[] terms) => new(1, "aw-split-panes", "v1", terms, Features: null, Matches: null);

    /// <summary>A pin-suggest answer for a pasted list: one match per concept key given (null for none), numbered from 0.</summary>
    internal static PinSuggestView Matched(params string?[] concepts) => new(1, "aw-split-panes", "v1", Terms: null, Features: null, [.. concepts.Select((c, i) => new PinSuggestMatch(i, c, c is { } key ? key["concept:".Length..] : null))]);

    /// <summary>The pin-suggest answer as the route serves it.</summary>
    internal static RouteAnswer Served(PinSuggestView view) => new(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(view, SindriApiClient.Json)));

    /// <summary>A pins answer: a waiting pin, an approved one and a removing one, with an idle rebuild.</summary>
    internal static PinsView Pinned() => new(1, "aw-split-panes", new string('b', 64), "v4",
    [
        new PinView("billing", "Billing", "Invoices and payment.", ["payments"], ["feature:invoices"], 0, "profile", PinStates.Approved, Change: null),
        new PinView("refunds", "Refunds", Summary: null, [], [], 2, "repo api", PinStates.Waiting, Change: null),
        new PinView("auth", "Sign in", Summary: null, [], [], 0, "profile", PinStates.Removing, Change: null),
    ],
    new MapRebuildView(MapRebuildPhases.Idle, Progress: null, RetryAt: null, Stale: false, Failed: null));

    /// <summary>The pins answer as the route serves it.</summary>
    internal static RouteAnswer Served(PinsView view) => new(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(view, SindriApiClient.Json)));

    /// <summary>A pasted-pages preview with one matched, ticked block and one new block with an unknown concept.</summary>
    internal static PagesPreview Previewed() => new("v41", new string('a', 64),
    [
        new PagesPreviewBlock(
            1, 1, "Schedule week", new PagesPreviewMatch("surface:route/GET:/schedule/week", "schedule-week", "Schedule week", 4), Replaces: null, SameAs: null, Ticked: true, Reason: null,
            [new PagesPreviewAction("Submit schedule", new PagesPreviewActionMatch("action:WeekPage.submit", "Submit schedule"), [new PagesPreviewConcept("self-scheduling", "self-scheduling")])], [],
            new PagesPreviewPage(Id: null, "Schedule week", "aw-split-panes", "surface:route/GET:/schedule/week",
                [new PagesPreviewPageAction(Id: null, "Submit schedule", "action:WeekPage.submit", ["self-scheduling"])])),
        new PagesPreviewBlock(
            2, 5, "Billing", Match: null, Replaces: null, SameAs: null, Ticked: false, "No concept called \"aproval\": pick one",
            [new PagesPreviewAction("Send invoice", Match: null, [new PagesPreviewConcept("aproval", Id: null)])], ["aproval"], Page: null),
    ]);

    /// <summary>The pasted-pages preview as the route serves it.</summary>
    internal static RouteAnswer Served(PagesPreview view) => new(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(view, SindriApiClient.Json)));
}
