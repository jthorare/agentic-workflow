using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3084: a "Did you mean" chip whose key holds underscores is drawn whole. Its visible text is the key itself, not a mnemonic-eaten copy.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextChipUnderscoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("aw_sindri_host")]
    [InlineData("_leading_and__double_")]
    public async Task A_did_you_mean_chip_shows_exactly_its_key(string key)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextChipUnderscoreTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var api = new FakeSindriApi
            {
                WholeMapAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.InternalServerError, "{}"),
                QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody([], [key]),
                LensesAnswer = _ => RouteAnswer.Problem(System.Net.HttpStatusCode.NotFound, "SND-CONTEXT-003", "no node is named that"),
            };
            var shell = new ShellViewModel(records, api, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell };
            try
            {
                window.Show();
                _ = shell.Run(stop.Token);
                for (var i = 0; i < 200 && shell.State != ShellState.Normal; i++)
                {
                    await Task.Delay(10, Token);
                    Dispatcher.UIThread.RunJobs();
                }

                shell.GoTo(PageId.Context);
                Dispatcher.UIThread.RunJobs();
                shell.Context.AnchorText = "zebra crossing";
                await shell.Context.Submit(Token);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                var chip = window.GetVisualDescendants().OfType<Button>().Single(b => string.Equals(AutomationProperties.GetName(b), key, StringComparison.Ordinal));
                var visible = Assert.Single(chip.GetVisualDescendants().OfType<TextBlock>());
                Assert.Equal(key, visible.Text);
                Assert.IsNotType<AccessText>(visible);
            }
            finally
            {
                await stop.CancelAsync();
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, Token);
    }
}
