using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The app's read of a pasted page list (<see cref="ISindriApi"/>, continued).</summary>
internal partial interface ISindriApi
{
    /// <summary>QUERY /sindri/context/pages/preview: reads a pasted list of pages against the map's pages and concepts; writes nothing. A server without the route answers 404, which the sheet reads as "can't read the list".</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="text">The paste (1 to 65,536 UTF-8 bytes).</param>
    /// <param name="repo">The repo to read and fill in, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One block per page, the API's refusal (SND-CONTEXT-259 for an empty or oversized paste, 258 for a repo it can't read), or SND-UI-002/003.</returns>
    public Task<Result<PagesPreview>> ContextPagesPreview(Uri api, string text, string? repo, CancellationToken cancellationToken);
}
