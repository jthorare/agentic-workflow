using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The pasted page list read (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<PagesPreview>> ContextPagesPreview(Uri api, string text, string? repo, CancellationToken cancellationToken) =>
        Call<PagesPreview>(api, HttpMethod.Query, ContextRoutes.PagesPreview, cancellationToken, new CallOptions(Content: AwJson.Serialize(new PagesPreviewBody(text, repo), Json)));
}
