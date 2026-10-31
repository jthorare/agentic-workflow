using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The pasted-pages preview route of the fake: not served (a 404) until a test serves it.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<PagesPreviewBody> _pagesPreviewAsked = [];

    /// <summary>POST /sindri/context/pages/preview's answer per paste (a 404 by default: a server without the route).</summary>
    public Func<PagesPreviewBody, RouteAnswer> ContextPagesPreviewAnswer { get; set; } = _ => _notServed;

    /// <summary>The pastes asked, in order.</summary>
    public IReadOnlyList<PagesPreviewBody> ContextPagesPreviewAsked { get { lock (_gate) { return [.. _pagesPreviewAsked]; } } }

    public Task<Result<PagesPreview>> ContextPagesPreview(Uri api, string text, string? repo, CancellationToken cancellationToken)
    {
        var body = new PagesPreviewBody(text, repo);
        lock (_gate)
        {
            _pagesPreviewAsked.Add(body);
        }

        return Task.FromResult(NotReceived<PagesPreview>(api) ?? Answer<PagesPreview>(api, "/sindri/context/pages/preview", ContextPagesPreviewAnswer(body)));
    }
}
