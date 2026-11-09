using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Memory;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Memory;

/// <summary>What the Memory tests share: the fake API holding a store called team-notes, a clock in 2026 and a page that has read the list.</summary>
internal sealed class MemoryFixture
{
    internal const string Store = "team-notes";

    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal static readonly DateTimeOffset Past = DateTimeOffset.UnixEpoch.AddHours(1);

    internal static readonly DateTimeOffset Future = new(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal FakeSindriApi Server { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:05:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal MemoryFixture()
    {
        _ = Server.SeedMemory(
            Store,
            FakeSindriApi.FactRow("f1", "Deploys go out on Tuesdays", ["deploys"]),
            FakeSindriApi.FactRow("f2", "Deploys need a second reviewer", ["deploys", "review"]),
            FakeSindriApi.FactRow("f3", "The old runbook is retired", ["runbook"], Past),
            FakeSindriApi.FactRow("f4", "Freeze starts in the new year", ["calendar"], Future));
    }

    internal MemoryPageViewModel Page() => new(Server, Clock);

    /// <summary>A page that is shown and has read the list once.</summary>
    internal async Task<MemoryPageViewModel> Shown(CancellationToken token)
    {
        var page = Page();
        page.SetShown(shown: true);
        await page.Refresh(Url, token);
        return page;
    }

    /// <summary>The team-notes row, open and searched for a word every seeded fact has no need to share: the query is the word given.</summary>
    internal static async Task<MemoryStoreRowViewModel> Searched(MemoryPageViewModel page, string query, CancellationToken token)
    {
        var store = Assert.Single(page.Stores, s => s.Name == Store);
        store.IsOpen = true;
        store.Search.Query = query;
        await store.Search.Run(token);
        return store;
    }
}
