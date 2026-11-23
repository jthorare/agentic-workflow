using Aw.Context.Core.Concepts;

namespace Aw.Context.Infrastructure.ConceptAnswers;

/// <summary>
/// A kept concepts answer as concept_answer.body holds it (review of PR 3881). <see cref="ConceptsView"/> leaves 'code' and 'next' out of its JSON when they are null, and its
/// constructor has no default for them, so it can't read its own JSON back under AwJson's required-parameter rule: every read would miss. This shape gives each such field a
/// default and is never an answer of any route, so no API type or golden changes.
/// </summary>
/// <param name="ApiVersion">The view's API version.</param>
/// <param name="Repo">The repo the answer is for, or null.</param>
/// <param name="MapVersion">The map version tag.</param>
/// <param name="State">The view's state.</param>
/// <param name="Concepts">The rows (no last change: it is laid over on every read).</param>
/// <param name="Other">The 'Other' row.</param>
/// <param name="Links">The links between concepts.</param>
/// <param name="CodePaths">Each row's code paths, by concept id.</param>
/// <param name="Code">The view's error code, null for an answer.</param>
/// <param name="Next">The next page's cursor, null on the last page.</param>
/// <param name="PagesInNoConcept">The view's count of pages in no concept, or null.</param>
internal sealed record StoredConcepts(
    int ApiVersion,
    string? Repo,
    string MapVersion,
    string State,
    IReadOnlyList<ConceptRow> Concepts,
    ConceptOther Other,
    IReadOnlyList<ConceptLink> Links,
    IReadOnlyDictionary<string, IReadOnlyList<string>> CodePaths,
    string? Code = null,
    string? Next = null,
    int? PagesInNoConcept = null)
{
    /// <summary>The body for a kept answer.</summary>
    internal static StoredConcepts Of(KeptConcepts kept)
    {
        var view = kept.View;
        return new(view.ApiVersion, view.Repo, view.MapVersion, view.State, view.Concepts, view.Other, view.Links, kept.Code, view.Code, view.Next, view.PagesInNoConcept);
    }

    /// <summary>The kept answer this body holds.</summary>
    internal KeptConcepts Kept() =>
        new(new ConceptsView(ApiVersion, Repo, MapVersion, State, Code, Concepts, Other, Links, Next) { PagesInNoConcept = PagesInNoConcept }, CodePaths);
}
