using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Infrastructure.Browser;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>
/// Chromium (installed once through the adapter's own installer; the first run downloads it), one shared
/// PlaywrightBrowser, and the fixture site on a free 127.0.0.1 port, with a second origin to navigate away to.
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    private LocalHttpServer? _site;

    internal PlaywrightBrowser Browser { get; } = new();

    internal LocalHttpServer Site => _site ?? throw new InvalidOperationException("the fixture isn't initialised");

    internal LocalHttpServer Elsewhere { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["/away"] = "<title>Away</title>" });

    public async ValueTask InitializeAsync()
    {
        var installed = await new PlaywrightInstaller().InstallAsync(CancellationToken.None);
        Assert.True(installed.IsSuccess, installed.IsError ? installed.Error.Message : null);
        _site = new LocalHttpServer(FixtureSite.Pages(Elsewhere.Origin));
    }

    public async ValueTask DisposeAsync()
    {
        await Browser.DisposeAsync();
        _site?.Dispose();
        Elsewhere.Dispose();
    }

    internal async Task<IBrowserSession> OpenAsync(string? recordDirectory = null, string? origin = null, IReadOnlyDictionary<string, string>? localStorage = null)
    {
        var host = HostGuard.Resolve(origin ?? Site.Origin, allowPreviewHost: false, previewHostPattern: null).Value;
        var session = await Browser.OpenAsync(new BrowserSessionOptions(host, new ViewportSize(1280, 800), recordDirectory, UserAgentOverride: null, localStorage), CancellationToken.None);
        Assert.True(session.IsSuccess, session.IsError ? session.Error.Message : null);
        return session.Value;
    }
}
