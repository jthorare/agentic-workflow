using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Lint;
using Aw.Kernel;
using Microsoft.Playwright;
using Locator = Aw.Evidence.Core.Browser.Locator;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>
/// One page in one Playwright context. Every failure is AW-EVIDENCE-013; disposing closes the context. The context's route
/// aborts an off-origin navigation, but Playwright never routes a redirect hop, so the session also watches where the
/// main frame actually is: once it has been anywhere off the vetted origin, every call fails (before and after it acts),
/// and sign-in checks again right before it types each credential.
/// </summary>
internal sealed partial class PlaywrightSession : IBrowserSession
{
    private static readonly TimeSpan _signInPage = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan _signInStep = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _closeLimit = TimeSpan.FromSeconds(10);
    private const float PathPollMs = 50;
    private readonly IBrowserContext _context;
    private readonly IPage _page;
    private readonly ResolvedHost _host;
    private readonly bool _recording;
    private string? _leftFor;

    internal PlaywrightSession(IBrowserContext context, IPage page, ResolvedHost host, bool recording, string browserVersion)
    {
        (_context, _page, _host, _recording, BrowserVersion) = (context, page, host, recording, browserVersion);
        page.FrameNavigated += (_, frame) =>
        {
            if (frame == page.MainFrame)
            {
                Note(frame.Url);
            }
        };
    }

    /// <inheritdoc/>
    public string BrowserVersion { get; }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> GotoAsync(string path, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"goto {path}", () => GotoCheckedAsync(path, timeout), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ClickAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"click {target}", async () =>
            await (await PlaywrightLocators.ResolveCheckedAsync(_page, target, timeout).ConfigureAwait(false))
                .ClickAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> FillAsync(Locator target, string value, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"fill {target}", async () =>
            await (await PlaywrightLocators.ResolveCheckedAsync(_page, target, timeout).ConfigureAwait(false))
                .FillAsync(value, new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitVisibleAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitAsync(target, WaitForSelectorState.Visible, timeout, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitHiddenAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitAsync(target, WaitForSelectorState.Hidden, timeout, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Polled in the page (as the sign-in's wait to leave the login page is), not through page.WaitForURLAsync: that reads the client's last-known URL and only then subscribes
    /// to the navigated event, both on the .NET side, while events arrive on the connection's reader thread; a navigation
    /// that lands between the two is missed and the wait runs to its timeout. A page-side poll is re-run in each new
    /// document by Playwright itself, so it can't miss a navigation that already happened.
    /// </remarks>
    public ValueTask<Result<Unit>> WaitForPathAsync(string path, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"wait for url path {path}", async () =>
        {
            var reached = await _page.WaitForFunctionAsync(
                PageScripts.OnPathLoaded, path, new() { PollingInterval = PathPollMs, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
            await reached.DisposeAsync().ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<string>> InputValueAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"read the value of {target}", () =>
            PlaywrightLocators.Resolve(_page, target).InputValueAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<Candidate>>> VisibleCandidatesAsync(CancellationToken cancellationToken) =>
        Guarded("list the visible candidates", () => PageCapture.VisibleCandidatesAsync(_page), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Box>> BoundingBoxAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"measure {target}", () => PageCapture.BoundingBoxAsync(PlaywrightLocators.Resolve(_page, target), timeout), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<byte[]>> ScreenshotAsync(CancellationToken cancellationToken) =>
        Guarded("screenshot", () => _page.ScreenshotAsync(new() { Type = ScreenshotType.Png }), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<PageSnapshot>> LintSnapshotAsync(CancellationToken cancellationToken) =>
        Guarded("capture the lint snapshot", () => PageCapture.LintSnapshotAsync(_page), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SettleAsync(TimeSpan settle, CancellationToken cancellationToken) =>
        Guarded("settle", async () =>
        {
            _ = await _page.EvaluateAsync<bool>(PageScripts.FontsReady).ConfigureAwait(false);
            await Task.Delay(settle, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DropFocusAsync(Offset? neutralClick, CancellationToken cancellationToken) =>
        Guarded("drop focus", async () =>
        {
            _ = await _page.EvaluateAsync<bool>(PageScripts.Blur).ConfigureAwait(false);
            if (neutralClick is not null && !await _page.EvaluateAsync<bool>(PageScripts.ControlAt, new { x = neutralClick.X, y = neutralClick.Y }).ConfigureAwait(false))
            {
                await _page.Mouse.ClickAsync((float)neutralClick.X, (float)neutralClick.Y).ConfigureAwait(false);
            }
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ResetScrollAsync(CancellationToken cancellationToken) =>
        Guarded("reset scroll", async () =>
        {
            _ = await _page.EvaluateAsync<bool>(PageScripts.ResetScroll).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SignInAsync(LoginSpec login, Credentials credentials, CancellationToken cancellationToken) =>
        Guarded("sign in", async () =>
        {
            _ = await _page.GotoAsync(_host.Origin, new() { WaitUntil = WaitUntilState.Load, Timeout = PlaywrightLocators.Ms(_signInPage) }).ConfigureAwait(false);
            ThrowIfOffOrigin();
            await FillWhenVisibleAsync(login.EmailSelector, credentials.Email, _signInPage).ConfigureAwait(false);
            if (login.NextSelector is { } next)
            {
                await _page.Locator(next).ClickAsync().ConfigureAwait(false);
            }

            await FillWhenVisibleAsync(login.PasswordSelector, credentials.Password, _signInStep).ConfigureAwait(false);
            await _page.Locator(login.SubmitSelector).ClickAsync(new() { Force = true }).ConfigureAwait(false);
            var left = await _page.WaitForFunctionAsync(
                PageScripts.OffPathLoaded, login.LoginPath, new() { PollingInterval = PathPollMs, Timeout = PlaywrightLocators.Ms(_signInStep) }).ConfigureAwait(false);
            await left.DisposeAsync().ConfigureAwait(false);
            return Unit.Value;
        }, cancellationToken, credentials.Redact);

    /// <inheritdoc/>
    public ValueTask<Result<SessionRecording>> StopRecordingAsync(string tracePath, CancellationToken cancellationToken) =>
        BrowserCall.Run("stop recording", async () =>
        {
            if (!_recording)
            {
                return new SessionRecording(Trace: null, Videos: []);
            }

            await _context.Tracing.StopAsync(new() { Path = tracePath }).ConfigureAwait(false);
            var video = _page.Video;
            await _page.CloseAsync().ConfigureAwait(false);
            await _context.CloseAsync().ConfigureAwait(false);   // a video file is complete only once its _context closes
            return new SessionRecording(tracePath, video is null ? [] : [await video.PathAsync().ConfigureAwait(false)]);
        }, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Bounded: CloseAsync finalizes the video and may never return from a wedged browser, and the locks are
            // released only after this; the browser itself is stopped at process exit.
            await _context.CloseAsync().WaitAsync(_closeLimit).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            // Already closed (StopRecordingAsync), the browser is gone, or it didn't close in time; nothing more to do here.
        }
    }

    private async Task GotoCheckedAsync(string path, TimeSpan timeout)
    {
        // Defence in depth behind the step guard: a path can't rewrite the host ('//evil.test', '@evil.test').
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new PlaywrightException($"a goto target must be a path on the host: {path}");
        }

        _ = await _page.GotoAsync(_host.UrlFor(path), new() { WaitUntil = WaitUntilState.Load, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
    }

    private ValueTask<Result<Unit>> WaitAsync(Locator target, WaitForSelectorState state, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"wait for {target} to be {state.ToString().ToLowerInvariant()}", () =>
            PlaywrightLocators.Resolve(_page, target).WaitForAsync(new() { State = state, Timeout = PlaywrightLocators.Ms(timeout) }), cancellationToken);

    private async Task FillWhenVisibleAsync(string css, string value, TimeSpan timeout)
    {
        var field = _page.Locator(css);
        await field.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
        // why: a handle is bound to the document it was checked in; a redirect that commits mid-fill detaches it and the
        // fill fails, where a Locator would retry into the new (possibly off-origin) document.
        var handle = await field.ElementHandleAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
        await using (handle.ConfigureAwait(false))
        {
            ThrowIfOffOrigin();   // never type a credential into a _page off the vetted origin
            await handle.FillAsync(value).ConfigureAwait(false);
        }
    }

    private ValueTask<Result<T>> Guarded<T>(string what, Func<Task<T>> call, CancellationToken cancellationToken, Func<string, string>? redact = null) =>
        BrowserCall.Run(what, async () =>
        {
            ThrowIfOffOrigin();
            var result = await call().ConfigureAwait(false);
            ThrowIfOffOrigin();
            return result;
        }, cancellationToken, redact);

    private ValueTask<Result<Unit>> Guarded(string what, Func<Task> call, CancellationToken cancellationToken) =>
        Guarded(what, async () =>
        {
            await call().ConfigureAwait(false);
            return Unit.Value;
        }, cancellationToken);

    // A chrome-error page is the browser's own page for a navigation it already failed (the route's abort among them).
    private void Note(string url)
    {
        if (!url.StartsWith("chrome-error:", StringComparison.Ordinal) && HostGuard.IsOffOriginNavigation(_host.Origin, url, isNavigation: true))
        {
            _ = Interlocked.CompareExchange(ref _leftFor, HostGuard.OriginOf(url), comparand: null);
        }
    }

    private void ThrowIfOffOrigin()
    {
        Note(_page.Url);
        if (Volatile.Read(ref _leftFor) is { } elsewhere)
        {
            throw new PlaywrightException($"the page left {_host.Origin} for {elsewhere} (a redirect or a script navigation); the session refuses to act there");
        }
    }
}
