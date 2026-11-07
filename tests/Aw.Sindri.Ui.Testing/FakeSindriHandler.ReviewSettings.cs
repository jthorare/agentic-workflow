using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The review settings route of <see cref="FakeSindriHandler"/> (Ph9): GET reads a project's settings, PUT saves them.</summary>
internal sealed partial class FakeSindriHandler
{
    private async Task<HttpResponseMessage> ReviewSettingsRoute(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!; // why: HttpClient always sets it on a sent request
        return request.Method == HttpMethod.Put
            ? Answer(Counted(await server.SaveReviewSettings(uri, await Body<ReviewSettingsSave>(request, cancellationToken), cancellationToken), server.ReviewSettingsSaveAnswer))
            : Answer(Counted(await server.GetReviewSettings(uri, Query(uri, "project") ?? string.Empty, cancellationToken), server.ReviewSettingsAnswer));
    }
}
