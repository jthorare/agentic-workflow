using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Controls;
using ChipTone = Aw.Sindri.Ui.Api.Workflow.ChipTone;
using Served = Aw.Sindri.Ui.Api.Workflow.StateChip;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The chip control under the headless platform: the word as served, the class its tone picks, hidden with nothing to say.</summary>
[Lane(TestLane.Integration)]
public sealed class StateChipViewTests
{
    private static readonly string[] _looks = ["chip", "attn", "fail", "ok"];

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StateChipViewTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (string? Word, string[] Classes, bool Visible) Draw(Served? served, Served? fallback = null)
    {
        var chip = new StateChipView { Chip = served, Fallback = fallback };
        var window = new Window { Content = chip, Width = 300, Height = 100 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var word = chip.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text;
        var result = (word, chip.Classes.Where(c => _looks.Contains(c, StringComparer.Ordinal)).Order(StringComparer.Ordinal).ToArray(), chip.IsVisible);
        window.Close();
        return result;
    }

    [Theory]
    [InlineData("PARKED", "attn", "attn")]
    [InlineData("FAILED", "fail", "fail")]
    [InlineData("RUNNING", "ok", "ok")]
    [InlineData("QUEUED", "neutral", null)]
    public async Task The_chip_shows_the_word_as_served_in_the_class_of_its_tone(string word, string tone, string? toneClass)
    {
        var (shown, classes, visible) = await OnUi(() => Draw(new Served(word, Enum.GetValues<ChipTone>().Single(t => string.Equals(t.ToString(), tone, StringComparison.OrdinalIgnoreCase)))));

        Assert.Equal(word, shown);
        string[] expected = toneClass is null ? ["chip"] : ["chip", toneClass];
        Assert.Equal(expected.Order(StringComparer.Ordinal), classes.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.True(visible);
    }

    // arch-4: the control is named for what it is, a view, so the served record keeps the name StateChip on its own.
    [Fact]
    public void The_control_is_StateChipView_and_no_control_shares_the_served_records_name()
    {
        var views = typeof(AppLifetime).Assembly.GetTypes().Where(t => t.Namespace == "Aw.Sindri.Ui.Views.Controls").Select(t => t.Name).ToList();

        Assert.Contains("StateChipView", views, StringComparer.Ordinal);
        Assert.DoesNotContain("StateChip", views, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_served_chip_wins_over_the_fallback_and_nothing_to_say_draws_nothing()
    {
        var (served, _, _) = await OnUi(() => Draw(new Served("PARKED", ChipTone.Attn), new Served("RUNNING", ChipTone.Ok)));
        var (fallback, fallbackClasses, _) = await OnUi(() => Draw(served: null, new Served("RUNNING", ChipTone.Ok)));
        var (_, _, visible) = await OnUi(() => Draw(served: null));

        Assert.Equal(("PARKED", "RUNNING"), (served, fallback));
        Assert.Contains(fallbackClasses, c => string.Equals(c, "ok", StringComparison.Ordinal));
        Assert.False(visible);
    }

    [Fact]
    public async Task A_later_chip_replaces_the_look_of_the_earlier_one()
    {
        var classes = await OnUi(() =>
        {
            var chip = new StateChipView { Chip = new Served("PARKED", ChipTone.Attn) };
            chip.Chip = new Served("DONE", ChipTone.Neutral);
            return chip.Classes.Order(StringComparer.Ordinal).ToArray();
        });

        Assert.Equal(["chip"], classes, StringComparer.Ordinal);
    }
}
