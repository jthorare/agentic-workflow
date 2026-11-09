using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Connections;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>What the Connections tests share: the in-memory API, the fake launcher, a clock the sign-in poll runs on, and three seeded connections.</summary>
internal sealed class ConnectionsFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal FakeSindriApi Server { get; } = new();

    internal FakeLauncher Launcher { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T01:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A key connection that failed its last test, an OAuth connection that works and an MCP one that needs a sign-in.</summary>
    internal void SeedThree()
    {
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("acme:key", "error", "rest", "the provider refused the refresh"));
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work") with { LastRefreshedAt = Clock.GetUtcNow() - TimeSpan.FromMinutes(30) });
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("notes:mcp", "needs-sign-in", "mcp", "the token expired"));
    }

    internal ConnectionActions Actions() => new(Server, () => Url, Launcher, Clock);

    internal ConnectionsPageViewModel Page() => new(Server, Launcher, Clock);

    /// <summary>A page that is shown and has read the list once.</summary>
    internal async Task<ConnectionsPageViewModel> Shown()
    {
        var page = Page();
        page.SetShown(shown: true);
        await page.Refresh(Url, Token);
        return page;
    }

    /// <summary>Lets a task that waits on the sign-in poll finish: the clock moves one poll at a time until it does.</summary>
    internal async Task Drive(Task task)
    {
        for (var i = 0; i < 5000 && !task.IsCompleted; i++)
        {
            await Task.Delay(1, Token);
            Clock.Advance(ConnectionActions.PollEvery);
        }

        await task;
    }
}
