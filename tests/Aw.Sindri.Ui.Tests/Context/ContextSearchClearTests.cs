using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.Views.Context;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>After a refused search the Context page shows one Clear search, the one beside the box (#4437).</summary>
[Lane(TestLane.Integration)]
public sealed class ContextSearchClearTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static async Task<string[]> VisibleClearButtons(string code, string detail)
    {
        var api = new FakeSindriApi { LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, code, detail) };
        return await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextSearchClearTests).Assembly).Dispatch(async () =>
        {
            var page = Page(api);
            var view = new ContextPage { DataContext = page };
            var window = new Window { Width = 1200, Height = 800, Content = view };
            try
            {
                window.Show();
                page.AnchorText = "Zzz";
                await page.Submit(Token);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                return view.GetVisualDescendants().OfType<Button>()
                    .Where(b => b.IsEffectivelyVisible && string.Equals(b.Content as string, Labels.ContextSearchClear, StringComparison.Ordinal))
                    .Select(b => b.Name ?? string.Empty)
                    .ToArray();
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }, Token);
    }

    [Fact]
    public async Task A_refused_search_shows_one_clear_search_beside_the_box()
    {
        var visible = await VisibleClearButtons("SND-CONTEXT-003", "no node is named 'Zzz'");

        Assert.Equal(["AnchorClear"], visible);
    }

    [Fact]
    public async Task A_search_naming_several_items_shows_one_clear_search_beside_the_box()
    {
        var visible = await VisibleClearButtons("SND-CONTEXT-037", "'Zzz' names several items");

        Assert.Equal(["AnchorClear"], visible);
    }
}
