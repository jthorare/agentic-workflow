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
}
