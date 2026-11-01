using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The concepts route of the fake: not served (a 404) until a test serves concepts, so every other Context test sees today's map.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<ConceptsQuery> _conceptsAsked = [];
    private readonly List<ConceptGlossaryQuery> _glossaryAsked = [];
    private readonly List<PinSuggestQuery> _pinSuggestAsked = [];
    private readonly List<PagesQuery> _pagesAsked = [];
    private readonly List<string> _pinsAsked = [];

    /// <summary>GET /sindri/context/pins's answer per repo asked (a 404 by default: a server without the route).</summary>
    public Func<string, RouteAnswer> ContextPinsAnswer { get; set; } = _ => _notServed;

    /// <summary>The pin reads asked (their repo), in order.</summary>
    public IReadOnlyList<string> ContextPinsAsked { get { lock (_gate) { return [.. _pinsAsked]; } } }

    /// <summary>GET /sindri/context/pages's answer per slice asked (a 404 by default: a server without the route).</summary>
    public Func<PagesQuery, RouteAnswer> ContextPagesAnswer { get; set; } = _ => _notServed;

    /// <summary>The page reads asked, in order.</summary>
    public IReadOnlyList<PagesQuery> ContextPagesAsked { get { lock (_gate) { return [.. _pagesAsked]; } } }

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

    /// <summary>GET /sindri/context/pins/suggest's answer per call (a 404 by default: a server without the route). A pasted list of more than 25 lines is asked in calls of 25, as the client sends it.</summary>
    public Func<PinSuggestQuery, RouteAnswer> ContextPinSuggestAnswer { get; set; } = _ => _notServed;

    /// <summary>The pin suggestions asked, one entry per call, in order.</summary>
    public IReadOnlyList<PinSuggestQuery> ContextPinSuggestAsked { get { lock (_gate) { return [.. _pinSuggestAsked]; } } }

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

    public Task<Result<PagesView>> ContextPages(Uri api, PagesQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _pagesAsked.Add(query);
        }

        return Task.FromResult(NotReceived<PagesView>(api) ?? Answer<PagesView>(api, "/sindri/context/pages", ContextPagesAnswer(query)));
    }

    public Task<Result<ConceptGlossaryView>> ContextConceptGlossary(Uri api, ConceptGlossaryQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _glossaryAsked.Add(query);
        }

        return Task.FromResult(NotReceived<ConceptGlossaryView>(api) ?? Answer<ConceptGlossaryView>(api, "/sindri/context/concepts/glossary", ContextConceptGlossaryAnswer(query)));
    }

    public Task<Result<PinSuggestView>> ContextPinSuggest(Uri api, PinSuggestQuery query, CancellationToken cancellationToken) =>
        PinSuggestBatches.Run(query, one =>
        {
            lock (_gate)
            {
                _pinSuggestAsked.Add(one);
            }

            return Task.FromResult(NotReceived<PinSuggestView>(api) ?? Answer<PinSuggestView>(api, "/sindri/context/pins/suggest", ContextPinSuggestAnswer(one)));
        });

    public Task<Result<PinsView>> ContextPins(Uri api, string repo, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _pinsAsked.Add(repo);
        }

        return Task.FromResult(NotReceived<PinsView>(api) ?? Answer<PinsView>(api, "/sindri/context/pins", ContextPinsAnswer(repo)));
    }
}
