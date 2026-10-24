using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Aw.Kernel;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;
using Aw.UiDriver.Core.Selectors;

namespace Aw.UiDriver.Avalonia;

/// <summary>
/// The verbs, over the app's window. Each one runs on the UI thread through the dispatcher, after a layout pass, so it sees the screen as it is
/// now; a wait polls from outside the UI thread so the app keeps running while it waits.
/// </summary>
/// <param name="window">The window to drive: the app's main window.</param>
/// <param name="clock">The clock waits run on.</param>
internal sealed class DriverHost(Func<Window?> window, TimeProvider clock)
{
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(50);

    internal Task<Result<TreeResponse>> Tree(TreeRequest request) => OnUi<TreeResponse>(tree =>
    {
        var start = request.From is { } origin ? tree.Resolve(origin) : Results.Success(tree.Nodes[0]);
        return start.IsError
            ? start.Error
            : new TreeResponse(tree.InfoOf(start.Value.Facts.Id, start.Value.Visual, Math.Clamp(request.Depth ?? TreeRequest.DefaultDepth, 0, 64)));
    });

    internal Task<Result<SearchResponse>> Search(SearchRequest request) => OnUi<SearchResponse>(tree =>
    {
        var found = SelectorMatcher.All(request.Selector, tree.Nodes.Select(static n => n.Facts));
        var limit = Math.Clamp(request.Limit ?? SearchRequest.DefaultLimit, 0, 10_000);
        return new SearchResponse([.. found.Take(limit).Select(f => tree.InfoOf(tree.Nodes.First(n => ReferenceEquals(n.Facts, f))))], found.Count);
    });

    internal Task<Result<PropsResponse>> Props(PropsRequest request) => OnUi(tree =>
        tree.Resolve(request.Selector).Map(node => new PropsResponse(tree.InfoOf(node), PropertyValues.Of(node.Visual))));

    internal Task<Result<ActedResponse>> Click(ClickRequest request) => Act(request.Selector, static (_, visual) => ElementActions.Click(visual));

    internal Task<Result<ActedResponse>> Action(ActionRequest request) => Act(request.Selector, (_, visual) => ElementActions.Do(visual, request.Action));

    internal Task<Result<ActedResponse>> Type(TypeRequest request) =>
        Act(request.Selector, (_, visual) => ElementActions.Type(visual, request.Text, request.Replace));

    internal Task<Result<ActedResponse>> Key(KeyRequest request) =>
        KeyChord.TryParse(request.Chord, out var chord)
            ? Act(request.Selector, (_, visual) => ElementActions.Press(visual, chord))
            : Task.FromResult<Result<ActedResponse>>(UiDriverErrors.Invalid.With($"'{request.Chord}' is not a key chord (Enter, Ctrl+K, Ctrl+Shift+P)"));

    internal Task<Result<ActedResponse>> Scroll(ScrollRequest request) =>
        Act(request.Selector, (_, visual) => ElementActions.Scroll(visual, request.Dx, request.Dy));

    internal Task<Result<WindowResponse>> WindowSize(WindowRequest request) => OnUi<WindowResponse>(tree =>
    {
        if (request.Width is < 1 or > 16_384 || request.Height is < 1 or > 16_384)
        {
            return UiDriverErrors.Invalid.With("width and height must be 1 to 16384");
        }

        var root = tree.Root;
        (root.Width, root.Height) = (request.Width ?? root.Width, request.Height ?? root.Height);
        root.UpdateLayout();
        // The platform window applies a new size on its own schedule; the answer is the size the window was given, else the size it has.
        return new WindowResponse(SizeOf(request.Width, root.Bounds.Width), SizeOf(request.Height, root.Bounds.Height), root.Title ?? string.Empty);
    });

    private static int SizeOf(int? given, double current) => given ?? (int)Math.Round(current, MidpointRounding.AwayFromZero);

    internal Task<Result<byte[]>> Screenshot(ScreenshotRequest request) => OnUi(tree =>
    {
        var target = request.Selector is { } selector ? tree.Resolve(selector).Map(static n => n.Visual) : Results.Success<Visual>(tree.Root);
        return target.Bind(visual => Pictures.Png(visual));
    });

    internal async Task<Result<WaitedResponse>> WaitFor(WaitForRequest request, CancellationToken cancellationToken)
    {
        var parsed = ParseWait(request);
        if (parsed.IsError)
        {
            return parsed.Error;
        }

        var state = parsed.Value;
        var limit = TimeSpan.FromMilliseconds(Math.Clamp(request.TimeoutMs ?? WaitForRequest.DefaultTimeoutMs, 0, 600_000));
        var started = clock.GetTimestamp();
        while (true)
        {
            var seen = await Look(request, state).ConfigureAwait(false);
            var waited = clock.GetElapsedTime(started);
            if (seen.IsError)
            {
                return seen.Error;
            }

            if (seen.Value.Holds)
            {
                return new WaitedResponse(seen.Value.Node, (int)waited.TotalMilliseconds);
            }

            if (waited >= limit)
            {
                var ms = ((int)limit.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
                return UiDriverErrors.TimedOut.With($"{request.Selector.Describe()} was not {state} within {ms} ms");
            }

            await Task.Delay(_poll, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Result<WaitState> ParseWait(WaitForRequest request)
    {
        if (!WaitStates.TryParse(request.State, out var state))
        {
            return UiDriverErrors.Invalid.With($"'{request.State}' is not a wait state (exists, visible, hidden, gone, enabled, disabled, textEquals, textContains)");
        }

        return WaitStates.NeedsText(state) && request.Text is null ? UiDriverErrors.Invalid.With($"{state} needs the request's text") : state;
    }

    private Task<Result<(NodeInfo? Node, bool Holds)>> Look(WaitForRequest request, WaitState state) => OnUi<(NodeInfo? Node, bool Holds)>(tree =>
    {
        var node = tree.Find(request.Selector);
        return (node is { } n ? tree.InfoOf(n) : null, WaitStates.Holds(state, node?.Facts, request.Text));
    });

    private Task<Result<ActedResponse>> Act(Selector? selector, Func<UiTree, Visual, Result<Unit>> work) => OnUi<ActedResponse>(tree =>
    {
        var target = selector is { } given ? tree.Resolve(given) : Focused(tree);
        if (target.IsError)
        {
            return target.Error;
        }

        var done = work(tree, target.Value.Visual);
        return done.IsError ? done.Error : new ActedResponse(tree.InfoOf(target.Value));
    });

    // The element holding keyboard focus, for a verb that names none.
    private static Result<(NodeFacts Facts, Visual Visual)> Focused(UiTree tree)
    {
        var focused = TopLevel.GetTopLevel(tree.Root)?.FocusManager?.GetFocusedElement();
        foreach (var node in tree.Nodes)
        {
            if (ReferenceEquals(node.Visual, focused))
            {
                return node;
            }
        }

        return UiDriverErrors.NoMatch.With("nothing has keyboard focus; name an element");
    }

    private async Task<Result<T>> OnUi<T>(Func<UiTree, Result<T>> work) =>
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (window() is not { } root)
            {
                return UiDriverErrors.Failed.With("the app has no window yet");
            }

            root.UpdateLayout();
            return work(new UiTree(root));
        });
}
