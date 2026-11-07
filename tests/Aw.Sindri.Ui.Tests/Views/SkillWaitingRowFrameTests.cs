using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.Views.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>A waiting skill's Needs you row drawn (skill flows spec §5.4): one line of text, a keyboard-reachable Details button that opens the question, and the hint line.</summary>
[Lane(TestLane.Integration)]
public sealed class SkillWaitingRowFrameTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouEntry Entry()
    {
        var list = AwJson.Deserialize<NeedsYouList>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("needs-you", "skill-waiting")), SindriApiClient.Json).Value;
        return NeedsYouMapper.Map(list, new CommandList([]), decisions: null, new HomeCounts(Running: 0, WithYou: 0, Queued: 0), readAt: A.Noon).Rows[0];
    }

    private static Task<bool> OnUi(Func<Task> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SkillWaitingRowFrameTests).Assembly).Dispatch(async () =>
        {
            await body();
            return true;
        }, Token);

    private static (Window Window, NeedsYouRow Row) Show(NeedsYouRowViewModel vm)
    {
        var row = new NeedsYouRow { DataContext = vm };
        var window = new Window { Width = 700, Content = row };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, row);
    }

    [Fact]
    public Task The_row_text_is_one_trimmed_line_and_Details_is_a_focusable_button_that_opens_the_question() => OnUi(() =>
    {
        var vm = new NeedsYouRowViewModel(Entry(), seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));
        var (window, row) = Show(vm);

        var line = row.FindControl<TextBlock>("SkillRowLine")!;
        Assert.Equal((1, TextTrimming.CharacterEllipsis, vm.SkillLine), (line.MaxLines, line.TextTrimming, line.Text));

        var details = row.FindControl<Button>("SkillDetailsButton")!;
        Assert.True(details.Focusable && details.IsTabStop && details.IsVisible);
        Assert.Equal("Details", details.Content);
        Assert.False(row.FindControl<StackPanel>("SkillDetailsPanel")!.IsVisible);

        details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(row.FindControl<StackPanel>("SkillDetailsPanel")!.IsVisible);
        Assert.Equal("Who calls this API?", row.FindControl<TextBlock>("SkillDetailsQuestion")!.Text);
        Assert.Equal("/run/.sindri-out/result-1.json", row.FindControl<TextBlock>("SkillDetailsPath")!.Text);
        window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task A_noResult_row_has_no_Details_button_and_the_hint_line_shows_only_on_the_row_that_carries_it() => OnUi(() =>
    {
        var entry = Entry();
        var noResult = entry with { OwnerWaiting = entry.OwnerWaiting! with { Reason = "noResult", Text = null } };
        var vm = new NeedsYouRowViewModel(noResult, seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));
        var (window, row) = Show(vm);

        Assert.False(row.FindControl<Button>("SkillDetailsButton")!.IsVisible);
        Assert.False(row.FindControl<TextBlock>("TakeoverHint")!.IsVisible);

        vm.ShowsTakeoverHint = true;
        Dispatcher.UIThread.RunJobs();
        var hint = row.FindControl<TextBlock>("TakeoverHint")!;
        Assert.True(hint.IsVisible);
        Assert.Equal("Take over opens this session in Warp. Hand back when you're done.", hint.Text);
        window.Close();
        return Task.CompletedTask;
    });
}
