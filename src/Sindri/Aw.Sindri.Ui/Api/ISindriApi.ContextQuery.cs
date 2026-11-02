using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>Sindri's HTTP API as the app uses it (<see cref="ISindriApi"/>, continued): the free-text context query.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/context/query?q= (context spec §8.5): a question in the person's words, read only for the anchors it resolves to.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="text">The question, as typed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="concept">A pin id to answer inside (<c xml:lang="en">concept=</c>), or null for the whole map.</param>
    /// <returns>The anchors and suggested anchors, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<QueryAnchorsView>> QueryAnchors(Uri api, string text, CancellationToken cancellationToken, string? concept = null);
}
