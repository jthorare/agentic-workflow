using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The concepts route of the fake: not served (a 404) until a test serves concepts, so every other Context test sees today's map.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<ConceptsQuery> _conceptsAsked = [];
    private readonly List<ConceptGlossaryQuery> _glossaryAsked = [];

    /// <summary>GET /sindri/context/concepts's answer per page asked (a 404 by default: a server without the route).</summary>
    public Func<ConceptsQuery, RouteAnswer> ContextConceptsAnswer { get; set; } = _ => _notServed;

    /// <summary>The concept reads asked, in order.</summary>
    public IReadOnlyList<ConceptsQuery> ContextConceptsAsked { get { lock (_gate) { return [.. _conceptsAsked]; } } }

    /// <summary>Held open until set: a concepts read that started while it was set waits for it.</summary>
    public TaskCompletionSource? ContextConceptsGate { get; set; }

    /// <summary>GET /sindri/context/concepts/glossary's answer (a 404 by default: a server without the route).</summary>
    public Func<ConceptGlossaryQuery, RouteAnswer> ContextConceptGlossaryAnswer { get; set; } = _ => _notServed;

    /// <summary>The glossary reads asked, in order.</summary>
    public IReadOnlyList<ConceptGlossaryQuery> ContextConceptGlossaryAsked { get { lock (_gate) { return [.. _glossaryAsked]; } } }

    // Counted apart from ContextCalls, which the Context page's tests hold at zero before a read of the lenses.
    public async Task<Result<ContextConceptsView>> ContextConcepts(Uri api, ConceptsQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _conceptsAsked.Add(query);
        }

        await WaitFor(ContextConceptsGate, cancellationToken).ConfigureAwait(false);
        return NotReceived<ContextConceptsView>(api) ?? Answer<ContextConceptsView>(api, "/sindri/context/concepts", ContextConceptsAnswer(query));
    }

    public Task<Result<ConceptGlossaryView>> ContextConceptGlossary(Uri api, ConceptGlossaryQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _glossaryAsked.Add(query);
        }

        return Task.FromResult(NotReceived<ConceptGlossaryView>(api) ?? Answer<ConceptGlossaryView>(api, "/sindri/context/concepts/glossary", ContextConceptGlossaryAnswer(query)));
    }
}
