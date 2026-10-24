using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Guard;
using Aw.Kernel;
using Microsoft.Playwright;
using IBrowser = Aw.Evidence.Core.Browser.IBrowser;
using IChromium = Microsoft.Playwright.IBrowser;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>
/// Headless Chromium through Playwright. One browser per launch profile (local, or preview with its private-network
/// switches) is started on first use and shared; each session gets its own context: the viewport at scale 1, the
/// desktop-Chrome user agent, video and tracing into the record directory, and a route that aborts every navigation
/// off the vetted origin.
/// </summary>
internal sealed class PlaywrightBrowser : IBrowser, IAsyncDisposable, IDisposable
{
    // A preview host is public, but the app it serves calls back into private address space; Chromium blocks that by default.
    private static readonly string[] _previewArgs =
        ["--disable-features=LocalNetworkAccessChecks,PrivateNetworkAccessRespectPreflightResults,BlockInsecurePrivateNetworkRequests"];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<HostKind, IChromium> _browsers = [];
    private IPlaywright? _playwright;

    /// <inheritdoc/>
    public async ValueTask<Result<IBrowserSession>> OpenAsync(BrowserSessionOptions options, CancellationToken cancellationToken)
    {
        var browser = await BrowserCall.Run("start chromium", () => LaunchedAsync(options.Host.Kind, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (browser.IsError && browser.Error.Message.Contains("Executable doesn't exist", StringComparison.Ordinal))
        {
            return browser.Error with { Message = browser.Error.Message + "; run 'ui-evidence install-browser'" };
        }

        return browser.IsError
            ? browser.Error
            : await BrowserCall.Run("open a browser context", () => OpenSessionAsync(browser.Value, options), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var browser in _browsers.Values)
            {
                try
                {
                    await browser.CloseAsync().ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                    // Already crashed or disconnected; close the others and stop the driver anyway.
                }
            }
        }
        finally
        {
            _playwright?.Dispose();
            _gate.Dispose();
        }
    }

    /// <inheritdoc/>
    /// <remarks>Stopping the Playwright driver ends the browsers it started; DisposeAsync closes them first.</remarks>
    public void Dispose()
    {
        _playwright?.Dispose();
        _gate.Dispose();
    }

    private static async Task<IBrowserSession> OpenSessionAsync(IChromium browser, BrowserSessionOptions options)
    {
        var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = options.Viewport.W, Height = options.Viewport.H },
            DeviceScaleFactor = 1,
            UserAgent = UserAgent.For(browser.Version, options.UserAgentOverride),
            RecordVideoDir = options.RecordDirectory,
            RecordVideoSize = options.RecordDirectory is null ? null : new() { Width = options.Viewport.W, Height = options.Viewport.H },
        }).ConfigureAwait(false);
        try
        {
            var origin = options.Host.Origin;
            await SeedLocalStorageAsync(context, options).ConfigureAwait(false);
            _ = await context.RouteAsync("**/*", route =>
                HostGuard.IsOffOriginNavigation(origin, route.Request.Url, route.Request.IsNavigationRequest)
                    ? route.AbortAsync("blockedbyclient")
                    : route.ContinueAsync()).ConfigureAwait(false);
            if (options.RecordDirectory is not null)
            {
                await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
            }

            var page = await context.NewPageAsync().ConfigureAwait(false);
            return new PlaywrightSession(context, page, options.Host, options.RecordDirectory is not null, browser.Version);
        }
        catch
        {
            try
            {
                // A half-opened context would otherwise live until the process exits; bounded, and never hiding the setup error.
                await context.CloseAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
            }

            throw;
        }
    }

    // Runs before any page script on every load of the host origin, so the app reads the overrides on its first render and after each navigation.
    private static async Task SeedLocalStorageAsync(IBrowserContext context, BrowserSessionOptions options)
    {
        if (options.LocalStorage is { Count: > 0 } entries)
        {
            _ = await context.AddInitScriptAsync(LocalStorageScript.For(options.Host.Origin, entries)).ConfigureAwait(false);
        }
    }

    private async Task<IChromium> LaunchedAsync(HostKind kind, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_browsers.TryGetValue(kind, out var browser))
            {
                _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
                browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true, Args = kind == HostKind.Preview ? _previewArgs : [] }).ConfigureAwait(false);
                _browsers[kind] = browser;
            }

            return browser;
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}
