using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
        var (window, card) = await OnFirstCard();
        var point = Corner(window, card);

        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle(window);

        Assert.Contains(":pressed", card.Classes, StringComparer.Ordinal);
        Assert.Equal(TokenColor("Brush.Clickable.Pressed"), ((ISolidColorBrush)card.Background!).Color);
        Assert.Equal(TokenColor("Brush.Clickable.PressedBorder"), ((ISolidColorBrush)card.BorderBrush!).Color);

        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle(window);
        Assert.DoesNotContain(":pressed", card.Classes, StringComparer.Ordinal);
        Assert.Equal(TokenColor("Brush.Clickable.Hover"), ((ISolidColorBrush)card.Background!).Color);
    });

    [Theory]
    [InlineData("pulsing")]
    [InlineData("steady")]
    [InlineData("subtle")]
    public Task The_pointer_over_a_tinted_Needs_You_row_shows_the_hover_background(string tint) => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Park(), NeedsYouRows.Failure(), NeedsYouRows.Profile()], ""));
        Settle(window);
        var row = window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)
            .SelectMany(r => r.GetVisualDescendants().OfType<Border>()).First(b => b.Name == "RowFrame" && b.Classes.Contains(tint));
        var rest = ((ISolidColorBrush)row.Background!).Color;

        window.MouseMove(row.TranslatePoint(new Point(4, 4), window)!.Value, RawInputModifiers.None);
        Settle(window);

        Assert.True(row.IsPointerOver);
        Assert.Equal(TokenColor("Brush.Clickable.Hover"), ((ISolidColorBrush)row.Background!).Color);
        Assert.NotEqual(rest, ((ISolidColorBrush)row.Background!).Color);
    });

    [Fact]
    public Task A_pressed_project_card_scales_to_098_about_its_centre_and_eases_it_over_Motion_Hover() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        var (window, card) = await OnFirstCard();
        var point = Corner(window, card);
        var transition = Assert.Single((card.Transitions ?? throw new InvalidOperationException("no Transitions")).OfType<TransformOperationsTransition>());
        Assert.Equal(150, transition.Duration.TotalMilliseconds);
        Assert.Equal(1.0, card.RenderTransform!.Value.M11, 3); // at rest: no scale

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
        Motion.Set(reduce: true); // the press lands at once; the row keeps its slides-in class (SlidesIn is read on arrival)
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
}
