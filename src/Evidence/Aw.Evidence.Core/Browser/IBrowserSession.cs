using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Lint;
using Aw.Kernel;

namespace Aw.Evidence.Core.Browser;

/// <summary>
/// One page in one browser context, driven by the run and parity use cases. Every failure (element missing, timeout,
/// crash) is AW-EVIDENCE-013 with a message that never carries a credential. Disposing closes the context.
/// </summary>
public interface IBrowserSession : IAsyncDisposable
{
    /// <summary>The browser build, for the user agent and the verdict-cache key.</summary>
    public string BrowserVersion { get; }

    /// <summary>Navigates to <paramref name="path"/> on the session's host and waits for load.</summary>
    /// <param name="path">A same-origin path (one leading '/').</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> GotoAsync(string path, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Clicks an element.</summary>
    /// <param name="target">The element.</param>
    /// <param name="timeout">How long to wait for it.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> ClickAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Replaces an input's value.</summary>
    /// <param name="target">The input.</param>
    /// <param name="value">The value.</param>
    /// <param name="timeout">How long to wait for it.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> FillAsync(Locator target, string value, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Waits until the element is visible.</summary>
    /// <param name="target">The element.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> WaitVisibleAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Waits until no visible element matches.</summary>
    /// <param name="target">The element.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> WaitHiddenAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Waits until the page's URL path equals <paramref name="path"/> (client-side routing can lag).</summary>
    /// <param name="path">The expected path.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> WaitForPathAsync(string path, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Scrolls an element's container (read-only), then lets lazy layout settle.</summary>
    /// <param name="target">The scroll container.</param>
    /// <param name="position">'end' or 'start' (horizontal), 'bottom' or 'top' (vertical), or a horizontal pixel offset.</param>
    /// <param name="timeout">How long to wait for it.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> ScrollAsync(Locator target, string position, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Drags <paramref name="source"/> onto <paramref name="destination"/> with real mouse events (press, a first nudge, a stepped move, a short
    /// hover, release), so pointer-sensor libraries see a genuine gesture. Both points are measured before the press: a missing
    /// source or destination fails with the mouse still up.
    /// </summary>
    /// <param name="source">The element to grab.</param>
    /// <param name="destination">The element to drop on.</param>
    /// <param name="gesture">The grab and drop points, and Alt.</param>
    /// <param name="timeout">How long to wait for each element.</param>
    /// <param name="cancellationToken">Cancels the drag.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> DragAsync(Locator source, Locator destination, DragGesture gesture, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Presses a key on <paramref name="target"/> (focused first), or on whatever has focus when there is none. A missing target sends no key.</summary>
    /// <param name="key">A Playwright key ('ArrowRight', 'Shift+Tab').</param>
    /// <param name="target">The element to focus, or null.</param>
    /// <param name="timeout">How long to wait for the target.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> PressAsync(string key, Locator? target, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Waits until one query parameter of the page's URL equals <paramref name="value"/>; the path is not checked.</summary>
    /// <param name="param">The parameter's name.</param>
    /// <param name="value">The expected value.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> WaitForUrlSearchAsync(string param, string value, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Looks once, now, for a visible element, without waiting for it to go.</summary>
    /// <param name="target">The element.</param>
    /// <param name="cancellationToken">Cancels the look.</param>
    /// <returns>Success when no visible element matches, else AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> ExpectAbsentNowAsync(Locator target, CancellationToken cancellationToken);

    /// <summary>Reads an input's current value.</summary>
    /// <param name="target">The input.</param>
    /// <param name="timeout">How long to wait for it.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The value or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<string>> InputValueAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Lists the currently visible interactive elements (buttons, links, inputs, role or test-id elements).</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The candidates in DOM order, or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<IReadOnlyList<Candidate>>> VisibleCandidatesAsync(CancellationToken cancellationToken);

    /// <summary>Measures an element's bounding box (two decimals).</summary>
    /// <param name="target">The element.</param>
    /// <param name="timeout">How long to wait for it.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The box, or AW-EVIDENCE-013 when the element isn't there.</returns>
    public ValueTask<Result<Box>> BoundingBoxAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Captures the viewport as PNG bytes.</summary>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>The PNG, or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<byte[]>> ScreenshotAsync(CancellationToken cancellationToken);

    /// <summary>Captures the elements and styles the lint rules read.</summary>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>The snapshot, or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<PageSnapshot>> LintSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>Waits for web fonts, then for <paramref name="settle"/>, so fades and late layout finish.</summary>
    /// <param name="settle">The extra wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> SettleAsync(TimeSpan settle, CancellationToken cancellationToken);

    /// <summary>Blurs the focused element, then clicks <paramref name="neutralClick"/> unless something interactive is there.</summary>
    /// <param name="neutralClick">Where to click; null only blurs.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> DropFocusAsync(Offset? neutralClick, CancellationToken cancellationToken);

    /// <summary>Scrolls the window and every scrolled container back to the top.</summary>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> ResetScrollAsync(CancellationToken cancellationToken);

    /// <summary>Signs in through the form <paramref name="login"/> describes and waits until the URL leaves its login path.</summary>
    /// <param name="login">The form's selectors and login path.</param>
    /// <param name="credentials">The credentials (never echoed in an error).</param>
    /// <param name="cancellationToken">Cancels the sign-in.</param>
    /// <returns>Success or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<Unit>> SignInAsync(LoginSpec login, Credentials credentials, CancellationToken cancellationToken);

    /// <summary>Stops tracing into <paramref name="tracePath"/>, closes the page and returns the recordings.</summary>
    /// <param name="tracePath">Where to write the trace archive.</param>
    /// <param name="cancellationToken">Cancels the stop.</param>
    /// <returns>The recordings (empty when the session records nothing), or AW-EVIDENCE-013.</returns>
    public ValueTask<Result<SessionRecording>> StopRecordingAsync(string tracePath, CancellationToken cancellationToken);
}
