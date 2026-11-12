using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: a shell over the fake server and a fake clock, so a test can leave a screen open, move the clock and ask whether the "N ago" words moved on the top bar's one-second beat.</summary>
internal sealed class AgoTickHost(DateTimeOffset start)
{
    internal static readonly DateTimeOffset GoldenDay = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture);

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    internal InMemoryEndpointRecords Records { get; } = new();

    internal FakeSindriApi Server { get; } = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    internal FakeTimeProvider Clock { get; } = new(start);

    internal async Task<ShellViewModel> Start()
    {
        Records.Write(A.Record());
        var shell = new ShellViewModel(Records, Server, Clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
        return shell;
    }

    internal static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    /// <summary>Sets every property called <paramref name="key"/> anywhere under <paramref name="node"/> to <paramref name="value"/>.</summary>
    internal static void SetEvery(JsonNode? node, string key, DateTimeOffset value)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in obj.Select(p => p.Key).ToList())
                {
                    if (string.Equals(name, key, StringComparison.Ordinal))
                    {
                        obj[name] = value.ToString("O", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        SetEvery(obj[name], key, value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    SetEvery(item, key, value);
                }

                break;
            default:
                break;
        }
    }
}
