using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>A Context page over a fake API, as the concept tests open it.</summary>
internal static class ConceptPage
{
    /// <summary>The page, made but not yet shown.</summary>
    internal static async Task<ContextPageViewModel> Made(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, TestContext.Current.CancellationToken)).Value);
        return new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
    }

    /// <summary>The page, shown.</summary>
    internal static async Task<ContextPageViewModel> Shown(FakeSindriApi api)
    {
        var page = await Made(api);
        await page.Opened(TestContext.Current.CancellationToken);
        return page;
    }

    /// <summary>A fake that serves concepts.</summary>
    internal static FakeSindriApi Serving(ContextConceptsView view) => new() { ContextConceptsAnswer = _ => AConcepts.Served(view) };
}
