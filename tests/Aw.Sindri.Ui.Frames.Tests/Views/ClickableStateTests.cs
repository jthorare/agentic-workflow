using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Clickable surfaces answer the pointer (#2535): the shared clickable class gives a hover and a pressed look from theme tokens, eased over Motion.Hover (150 ms), instantly under reduced motion. The project card is the first user.</summary>
[Lane(TestLane.Integration)]
public sealed class ClickableStateTests : FrameHost
{
    private async Task<(Window Window, Border Card)> OnFirstCard()
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        var card = window.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "ProjectCardFrame", StringComparison.Ordinal) && b.IsEffectivelyVisible);
        return (window, card);
    }

    private static Color TokenColor(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException($"{key} is not a brush token");

    /// <summary>
    /// Moves the pointer to <paramref name="at"/> until <paramref name="target"/> is under it, and returns the last point. The point is read
    /// again on every try: a row still sliding in (180 ms) sits higher than it ends, and a pressed card takes focus, which scrolls it into
    /// view. Headless hit-tests the last rendered scene, renders one only on a forced tick, and its animation clock reads real time on each
    /// tick, so wait real time between tries (at most 600 ms).
    /// </summary>
    private static async Task<Point> PointAt(Window window, Func<Point> at, InputElement target)
    {
        var point = at();
        for (var tick = 0; tick < 20 && !target.IsPointerOver; tick++)
        {
            await Task.Delay(30, Token);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Settle(window);
            point = at();
            window.MouseMove(point, RawInputModifiers.None);
            Settle(window);
        }

        return point;
    }

    private static string Describe(IInputElement? hit) =>
        hit is Visual visual
            ? string.Join(" < ", visual.GetSelfAndVisualAncestors().Take(6).Select(v => $"{v.GetType().Name}#{(v as Control)?.Name}[{(v as Control)?.DataContext?.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture)}]"))
            : "nothing";

    private static string Chain(Visual target) =>
        string.Join(" | ", target.GetSelfAndVisualAncestors().Where(v => v.RenderTransform is not null && !v.RenderTransform.Value.IsIdentity)
            .Select(v => $"{v.GetType().Name}#{(v as Control)?.Name} {v.RenderTransform!.Value} origin {v.RenderTransformOrigin}"))
        + $" ; classes {string.Join(',', (target as StyledElement)?.Classes ?? [])} ; dc {(target as Control)?.DataContext?.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private static Point Corner(Window window, Border card) => card.TranslatePoint(new Point(6, 6), window)!.Value; // the card's own padding, no control in it

    [Fact]
    public Task A_project_card_carries_the_shared_class_and_eases_its_colours_over_Motion_Hover() => OnUi(async () =>
    {
        var (_, card) = await OnFirstCard();

        Assert.Contains("clickable", card.Classes, StringComparer.Ordinal);
        Assert.NotNull(card.Cursor);
        var transitions = card.Transitions ?? throw new InvalidOperationException("the card has no Transitions");
        foreach (var property in new AvaloniaProperty[] { Border.BackgroundProperty, Border.BorderBrushProperty })
        {
            var transition = Assert.Single(transitions.OfType<BrushTransition>(), t => Equals(t.Property, property));
            Assert.Equal(150, transition.Duration.TotalMilliseconds);
        }
    });

    [Fact]
    public Task The_pointer_over_a_project_card_lifts_its_background_and_border_to_the_hover_tokens() => OnUi(async () =>
    {
        Motion.Set(reduce: true); // no easing: the value is the end value at once
        using var instant = InstantMotion.Start();
        var (window, card) = await OnFirstCard();
        var rest = ((ISolidColorBrush)card.Background!).Color;

        window.MouseMove(Corner(window, card), RawInputModifiers.None);
        Settle(window);

        Assert.True(card.IsPointerOver);
        Assert.Equal(TokenColor("Brush.Clickable.Hover"), ((ISolidColorBrush)card.Background!).Color);
        Assert.Equal(TokenColor("Brush.Clickable.HoverBorder"), ((ISolidColorBrush)card.BorderBrush!).Color);
        Assert.NotEqual(rest, ((ISolidColorBrush)card.Background!).Color);
    });

    [Fact]
    public Task A_pressed_project_card_takes_the_pressed_tokens_and_lets_go_on_release() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, card) = await OnFirstCard();
        var point = Corner(window, card);

        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle(window);

        Assert.Contains(":pressed", card.Classes, StringComparer.Ordinal);
        Assert.Equal(TokenColor("Brush.Clickable.Pressed"), ((ISolidColorBrush)card.Background!).Color);
        Assert.Equal(TokenColor("Brush.Clickable.PressedBorder"), ((ISolidColorBrush)card.BorderBrush!).Color);

        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        point = await PointAt(window, () => Corner(window, card), card); // the press near the edge left the shrunk card, and its focus scrolled it into view
        Assert.DoesNotContain(":pressed", card.Classes, StringComparer.Ordinal);
        Assert.True(card.IsPointerOver, $"at {point}, hit {Describe(window.InputHitTest(point))}, chain {Chain(card)}, at now {Corner(window, card)}");
        Assert.Equal(TokenColor("Brush.Clickable.Hover"), ((ISolidColorBrush)card.Background!).Color);
    });

    [Fact]
    public Task Every_list_row_frame_is_clickable() => OnUi(() =>
    {
        // Every row that opens something takes the shared look (#2535 review): the row views, and Linear's row from its page's item template.
        var frames = new (Control View, string Frame)[]
        {
            (new Aw.Sindri.Ui.Views.Queue.ItemRow(), "RowFrame"),
            (new Aw.Sindri.Ui.Views.Queue.CompactItemRow(), "RowFrame"),
            (new Aw.Sindri.Ui.Views.Jobs.JobRow(), "JobRowFrame"),
            (new Aw.Sindri.Ui.Views.Connections.ConnectionRow(), "ConnectionRowFrame"),
            (new Aw.Sindri.Ui.Views.Agents.SessionRow(), "SessionRowFrame"),
            (new Aw.Sindri.Ui.Views.Agents.EndedSessionRow(), "EndedRowFrame"),
            (new NeedsYouRow(), "RowFrame"),
        };
        foreach (var (view, name) in frames)
        {
            var frame = view.FindControl<Border>(name) ?? throw new InvalidOperationException($"{view.GetType().Name} has no {name}");
            Assert.True(frame.Classes.Contains("clickable"), $"{view.GetType().Name}.{name} is not clickable");
        }

        // The row sits two templates deep (groups, then rows), so each built template is searched for the next list.
        var linear = Templated(new Aw.Sindri.Ui.Views.Linear.LinearWorkView()).OfType<Border>()
            .Single(b => string.Equals(b.Name, "LinearRowFrame", StringComparison.Ordinal));
        Assert.Contains("clickable", linear.Classes, StringComparer.Ordinal);
        return Task.CompletedTask;
    });

    [Fact]
    public Task A_selected_card_lifts_one_step_more_under_the_pointer() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, card) = await OnFirstCard();
        card.Classes.Add("selected"); // a selected row rests on Surface2, the hover fill

        _ = await PointAt(window, () => Corner(window, card), card);

        Assert.Equal(TokenColor("Brush.Surface3"), ((ISolidColorBrush)card.Background!).Color);
    });

    [Fact]
    public Task A_card_with_the_keyboard_ring_keeps_it_while_pressed() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, card) = await OnFirstCard();
        Assert.True(card.Focus(NavigationMethod.Tab));
        Settle(window);
        Assert.Contains(":focus-visible", card.Classes, StringComparer.Ordinal);

        ((IPseudoClasses)card.Classes).Set(":pressed", value: true);
        Settle(window);

        Assert.Equal(TokenColor("Brush.PrimaryHover"), ((ISolidColorBrush)card.BorderBrush!).Color);
    });

    [Fact]
    public Task A_press_that_starts_on_a_child_holds_when_the_pointer_leaves_and_ends_on_release_outside() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, card) = await OnFirstCard();
        var text = card.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.IsEffectivelyVisible && !t.GetVisualAncestors().TakeWhile(a => !ReferenceEquals(a, card)).OfType<Button>().Any());
        text.Background = Brushes.Transparent; // a TextBlock without a fill never takes the hit; a chip or a filled label does
        // The row is still sliding in, so the label's point is read again until the pointer is on it.
        var on = await PointAt(window, () => text.TranslatePoint(new Point(2, text.Bounds.Height / 2), window)!.Value, text);
        Assert.True(ReferenceEquals(text, window.InputHitTest(on)), $"the press lands on {Describe(window.InputHitTest(on))}, not the label");

        window.MouseDown(on, MouseButton.Left, RawInputModifiers.None);
        Settle(window);
        var away = new Point(1, 1);
        window.MouseMove(away, RawInputModifiers.LeftMouseButton);
        Settle(window);

        Assert.Contains(":pressed", card.Classes, StringComparer.Ordinal);

        window.MouseUp(away, MouseButton.Left, RawInputModifiers.None);
        Settle(window);
        Assert.DoesNotContain(":pressed", card.Classes, StringComparer.Ordinal);
    });

    [Theory]
    [InlineData("pulsing")]
    [InlineData("steady")]
    [InlineData("subtle")]
    public Task The_pointer_over_a_tinted_Needs_You_row_shows_the_hover_background(string tint) => OnUi(async () =>
    {
        using var instant = InstantMotion.Start(); // no easing, but motion stays on: under reduced motion an unseen park row is outlined, not pulsing
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Park(), NeedsYouRows.Failure(), NeedsYouRows.Profile()], ""));
        Settle(window);
        var row = window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)
            .SelectMany(r => r.GetVisualDescendants().OfType<Border>()).First(b => b.Name == "RowFrame" && b.Classes.Contains(tint));
        var rest = ((ISolidColorBrush)row.Background!).Color;

        var at = await PointAt(window, () => row.TranslatePoint(new Point(4, 4), window)!.Value, row);

        Assert.True(row.IsPointerOver, $"at {at}, row bounds {row.Bounds}, hit {Describe(window.InputHitTest(at))}, chain {Chain(row)}, at now {row.TranslatePoint(new Point(4, 4), window)}");
        Assert.Equal(TokenColor(tint == "subtle" ? "Brush.Surface3" : "Brush.Clickable.Hover"), ((ISolidColorBrush)row.Background!).Color); // a calm row rests on the hover fill, so it lifts one step more
        Assert.NotEqual(rest, ((ISolidColorBrush)row.Background!).Color);
    });

    [Fact]
    public Task A_pressed_project_card_scales_to_098_about_its_centre_and_eases_it_over_Motion_Hover() => OnUi(async () =>
    {
        var (window, card) = await OnFirstCard();
        var point = Corner(window, card);
        var transition = Assert.Single((card.Transitions ?? throw new InvalidOperationException("no Transitions")).OfType<TransformOperationsTransition>());
        Assert.Equal(150, transition.Duration.TotalMilliseconds);
        Assert.Equal(1.0, card.RenderTransform!.Value.M11, 3); // at rest: no scale

        Motion.Set(reduce: true); // the press lands at once: the transition reads Motion.Hover again as the pointer arrives
        using var instant = InstantMotion.Start();

        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle(window);

        var pressed = card.RenderTransform!.Value;
        Assert.Equal((0.98, 0.98), (Math.Round(pressed.M11, 3, MidpointRounding.AwayFromZero), Math.Round(pressed.M22, 3, MidpointRounding.AwayFromZero)));
        Assert.Equal(RelativePoint.Center, card.RenderTransformOrigin);

        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle(window);
        Assert.Equal(1.0, card.RenderTransform!.Value.M11, 3);
    });

    [Fact]
    public Task Under_reduced_motion_the_pointer_arriving_sets_every_clickable_transition_to_zero() => OnUi(async () =>
    {
        var (window, card) = await OnFirstCard();
        var transitions = card.Transitions ?? throw new InvalidOperationException("the card has no Transitions");
        Assert.All(transitions, t => Assert.Equal(150, Length(t).TotalMilliseconds)); // wired before motion was reduced

        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        window.MouseMove(Corner(window, card), RawInputModifiers.None);
        Settle(window);

        Assert.True(card.IsPointerOver);
        Assert.Equal(3, transitions.Count);
        Assert.All(transitions, t => Assert.Equal(TimeSpan.Zero, Length(t)));
    });

    [Fact]
    public Task A_Needs_You_row_that_slid_in_scales_to_098_while_pressed() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Failure()], ""));
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Failure(), NeedsYouRows.Park()], ""));
        Settle(window);
        var row = window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)
            .SelectMany(r => r.GetVisualDescendants().OfType<Border>()).First(b => b.Name == "RowFrame" && b.Classes.Contains("slides-in"));
        await Task.Delay(250, Token); // past the 180 ms slide
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); // headless moves the animation clock only on a forced tick
        Settle(window);
        using var instant = InstantMotion.Start(); // the press lands at once; motion stays on, since reduced motion takes the slides-in class away
        var point = row.TranslatePoint(new Point(4, 4), window)!.Value;

        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle(window);

        Assert.Contains(":pressed", row.Classes, StringComparer.Ordinal);
        var pressed = row.RenderTransform!.Value;
        Assert.Equal((0.98, 0.98), (Math.Round(pressed.M11, 3, MidpointRounding.AwayFromZero), Math.Round(pressed.M22, 3, MidpointRounding.AwayFromZero)));
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
    });

    private static TimeSpan Length(ITransition transition) => transition switch
    {
        BrushTransition brush => brush.Duration,
        TransformOperationsTransition transform => transform.Duration,
        _ => throw new InvalidOperationException($"unexpected transition {transition.GetType().Name}"),
    };

    private static IEnumerable<Control> Templated(Control root)
    {
        foreach (var list in root.GetLogicalDescendants().OfType<ItemsControl>().ToList())
        {
            if (list.ItemTemplate?.Build(param: null) is { } built)
            {
                yield return built;
                foreach (var inner in Templated(built))
                {
                    yield return inner;
                }
            }
        }
    }
}
