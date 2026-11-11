using System.Net;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using Aw.Sindri.Ui.Views.Todo;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The From elsewhere line drawn (todo spec §3.1, §3.4, §3.5): its words and links, its names for assistive technology, and its single focus stop.</summary>
[Lane(TestLane.Integration)]
public sealed class FromElsewhereLineTests
{
    private static readonly Uri _url = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<bool> OnUi(Func<Task> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FromElsewhereLineTests).Assembly).Dispatch(async () =>
        {
            await body();
            return true;
        }, Token);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static Window Host(FromElsewhereViewModel line)
    {
        var window = new Window { Width = 720, Height = 120, Content = new FromElsewhereLine { DataContext = line } };
        window.Show();
        Settle(window);
        return window;
    }

    private static List<Button> Links(Window window) => [.. window.GetVisualDescendants().OfType<Button>()];

    private static Border Frame(Window window) => window.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "ElsewhereFrame", StringComparison.Ordinal));

    private async Task<FromElsewhereViewModel> Loaded(int reviews = 2, bool reviewsFailed = false)
    {
        var line = new FromElsewhereViewModel(_server);
        line.SetReviews(loading: false, reviews, reviewsFailed);
        await line.Refresh(_url, Token);
        return line;
    }

    [Fact]
    public Task It_draws_a_link_per_part_with_its_name_for_assistive_technology() => OnUi(async () =>
    {
        var window = Host(await Loaded());

        var links = Links(window);

        Assert.Equal(
            ["Opens Reviews: 2 PRs waiting on your review", "Opens Linear: 3 Linear issues assigned"],
            links.Select(AutomationProperties.GetName),
            StringComparer.Ordinal);
        Assert.All(links, b => Assert.True(b.IsEffectivelyVisible && b.Bounds.Width > 0));
        window.Close();
    });

    [Fact]
    public Task The_line_is_one_focus_stop_and_its_parts_are_not() => OnUi(async () =>
    {
        var window = Host(await Loaded());

        var frame = Frame(window);

        Assert.True(frame.Focusable && frame.IsTabStop);
        Assert.All(Links(window), b => Assert.False(b.Focusable));
        Assert.Equal("From elsewhere", AutomationProperties.GetName(frame));
        window.Close();
    });

    [Fact]
    public Task Before_the_reads_answer_it_draws_the_checking_line_with_no_link() => OnUi(async () =>
    {
        var window = Host(new FromElsewhereViewModel(_server));

        Assert.Empty(Links(window));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => string.Equals(t.Text, "Checking reviews and Linear…", StringComparison.Ordinal));
        await Task.CompletedTask;
        window.Close();
    });

    [Fact]
    public Task With_nothing_waiting_it_says_so_and_keeps_its_height() => OnUi(async () =>
    {
        _server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "filtered-empty");
        var line = await Loaded(reviews: 0);
        var window = Host(new FromElsewhereViewModel(_server));
        var checking = Frame(window).Bounds.Height;
        window.Close();

        window = Host(line);

        Assert.Empty(Links(window));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => string.Equals(t.Text, "Nothing waiting elsewhere", StringComparison.Ordinal));
        Assert.Equal(checking, Frame(window).Bounds.Height);
        window.Close();
    });

    [Fact]
    public Task A_failed_part_draws_its_line_and_a_named_Retry() => OnUi(async () =>
    {
        _server.LinearIssuesAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-X-500", "down");
        var window = Host(await Loaded(reviewsFailed: true));

        var names = Links(window).Select(AutomationProperties.GetName).ToList();

        Assert.Equal(
            [
                "Opens Reviews: Reviews didn't load", "Retry Reviews",
                "Opens Linear: Linear didn't load", "Retry Linear",
            ],
            names,
            StringComparer.Ordinal);
        window.Close();
    });

    [Fact]
    public Task Right_and_Enter_open_the_second_part_from_the_keyboard() => OnUi(async () =>
    {
        var line = await Loaded();
        var opened = new List<ElsewherePart>();
        line.OpenRequested += (_, e) => opened.Add(e.Part);
        var window = Host(line);
        _ = Frame(window).Focus();
        Settle(window);

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.Equal([ElsewherePart.Linear], opened);
        window.Close();
    });

    [Fact]
    public Task X_Delete_and_Space_do_nothing_on_the_line() => OnUi(async () =>
    {
        var line = await Loaded();
        var opened = 0;
        line.OpenRequested += (_, _) => opened++;
        var window = Host(line);
        _ = Frame(window).Focus();
        Settle(window);

        window.KeyPressQwerty(PhysicalKey.X, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);

        Assert.Equal(0, opened);
        window.Close();
    });
}
