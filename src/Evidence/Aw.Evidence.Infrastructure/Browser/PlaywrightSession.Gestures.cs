using System.Globalization;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Kernel;
using Microsoft.Playwright;
using Locator = Aw.Evidence.Core.Browser.Locator;

namespace Aw.Evidence.Infrastructure.Browser;

// The script-feature gestures: scroll, drag, press, the URL-search wait and the absence check.
internal sealed partial class PlaywrightSession
{
    private const double DragActivationPx = 10;   // dnd-kit's pointer sensor activates after 6px of travel: the first move clears that
    private const int DragSteps = 12;
    private static readonly TimeSpan _dragHover = TimeSpan.FromMilliseconds(150);   // lets the drop target's hover state render before the release
    private static readonly TimeSpan _scrollSettle = TimeSpan.FromMilliseconds(300);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ScrollAsync(Locator target, string position, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"scroll {target}", async () =>
        {
            var container = PlaywrightLocators.Resolve(_page, target);
            await container.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
            _ = await container.EvaluateAsync(PageScripts.ScrollTo, position).ConfigureAwait(false);
            _ = await _page.EvaluateAsync<bool>(PageScripts.NextFrame).ConfigureAwait(false);
            await Task.Delay(_scrollSettle, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DragAsync(Locator source, Locator destination, DragGesture gesture, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"drag {source} to {destination}", () => DragCheckedAsync(source, destination, gesture, timeout, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> PressAsync(string key, Locator? target, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"press {key}", async () =>
        {
            if (target is not null)
            {
                await (await PlaywrightLocators.ResolveCheckedAsync(_page, target, timeout).ConfigureAwait(false))
                    .FocusAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
            }

            await _page.Keyboard.PressAsync(key).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitForUrlSearchAsync(string param, string value, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guarded($"wait for url ?{param}={value}", async () =>
        {
            var reached = await _page.WaitForFunctionAsync(
                PageScripts.OnSearchParam, new { param, value }, new() { PollingInterval = PathPollMs, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
            await reached.DisposeAsync().ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ExpectAbsentNowAsync(Locator target, CancellationToken cancellationToken) =>
        Guarded($"expect {target} absent", async () =>
        {
            var visible = await PlaywrightLocators.Visible(_page, target).CountAsync().ConfigureAwait(false);
            if (visible > 0)
            {
                throw new PlaywrightException(string.Create(CultureInfo.InvariantCulture, $"expected no visible match, found {visible}"));
            }
        }, cancellationToken);

    // Real mouse events, not synthetic drag events. Both points are measured before the press: a missing source or destination throws with the mouse still up.
    private async Task DragCheckedAsync(Locator source, Locator destination, DragGesture gesture, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var from = PlaywrightLocators.FirstVisible(_page, source);
        await from.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
        await from.ScrollIntoViewIfNeededAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
        var to = PlaywrightLocators.FirstVisible(_page, destination);
        await to.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
        var (fromX, fromY) = await PointInAsync(from, gesture.Grab, timeout).ConfigureAwait(false);
        var (toX, toY) = await PointInAsync(to, gesture.Drop, timeout).ConfigureAwait(false);
        var (dx, dy) = (toX - fromX, toY - fromY);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        // Nudge toward the destination (or right, for a drop on the grab point).
        var (nudgeX, nudgeY) = length == 0 ? (DragActivationPx, 0) : (dx / length * DragActivationPx, dy / length * DragActivationPx);

        await _page.Mouse.MoveAsync((float)fromX, (float)fromY).ConfigureAwait(false);
        await _page.Mouse.DownAsync().ConfigureAwait(false);
        try
        {
            await _page.Mouse.MoveAsync((float)(fromX + nudgeX), (float)(fromY + nudgeY)).ConfigureAwait(false);
            await _page.Mouse.MoveAsync((float)toX, (float)toY, new() { Steps = DragSteps }).ConfigureAwait(false);
            await Task.Delay(_dragHover, cancellationToken).ConfigureAwait(false);
            if (gesture.HoldAlt)
            {
                await _page.Keyboard.DownAsync("Alt").ConfigureAwait(false);
            }
        }
        finally
        {
            await _page.Mouse.UpAsync().ConfigureAwait(false);
            if (gesture.HoldAlt)
            {
                await _page.Keyboard.UpAsync("Alt").ConfigureAwait(false);
            }
        }
    }

    private static async Task<(double X, double Y)> PointInAsync(ILocator element, Offset? offset, TimeSpan timeout)
    {
        var box = await element.BoundingBoxAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false)
            ?? throw new PlaywrightException("element has no bounding box");
        return (box.X + (offset?.X ?? (box.Width / 2)), box.Y + (offset?.Y ?? (box.Height / 2)));
    }
}
