using System.Net;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The free-text context query of the fake: the Context box asks it before it guesses anchors.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string> _contextLog = [];

    /// <summary>GET /sindri/context/query's answer per question: no anchors and no suggestions by default.</summary>
    public Func<string, RouteAnswer> QueryAnswer { get; set; } = _ => QueryAnchorsBody([], []);

    /// <summary>Held open until set: the query waits for it (a question in flight); null answers at once.</summary>
    public TaskCompletionSource? QueryGate { get; set; }

    /// <summary>Every lenses and query read, in order, as "lenses:a b" and "query:text".</summary>
    public IReadOnlyList<string> ContextLog { get { lock (_gate) { return [.. _contextLog]; } } }

    /// <summary>The query's answer for these anchors and suggestions.</summary>
    /// <param name="anchors">The resolved anchors.</param>
    /// <param name="suggested">The suggested anchors.</param>
    /// <returns>A 200 with the two lists.</returns>
    internal static RouteAnswer QueryAnchorsBody(IEnumerable<string> anchors, IEnumerable<string> suggested) =>
        new(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new { anchors, suggestedAnchors = suggested }));

    public Task<Result<QueryAnchorsView>> QueryAnchors(Uri api, string text, CancellationToken cancellationToken, string? concept = null)
    {
        Log(concept is null ? "query:" + text : $"query:{text} concept={concept}");
        return Context<QueryAnchorsView>(api, "/sindri/context/query", QueryAnswer(text), QueryGate, cancellationToken);
    }

    private void Log(string entry)
    {
        lock (_gate)
        {
            _contextLog.Add(entry);
        }
    }
}
