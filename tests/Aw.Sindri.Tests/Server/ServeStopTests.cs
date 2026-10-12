using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Server;

/// <summary>'sindri serve' as the CLI runs it, stopped through the API while a client follows the event stream.</summary>
public sealed class ServeStopTests : IDisposable
{
    private static readonly TimeSpan _prompt = TimeSpan.FromSeconds(5);   // well under Kestrel's 30 s graceful-shutdown wait
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_stop_ends_every_event_stream_and_serve_exits_promptly()
    {
        var ct = TestContext.Current.CancellationToken;
        var serve = ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => "0", _ => null }, TimeProvider.System, TextWriter.Null);
        var url = await Endpoint(ct);
        using var http = new HttpClient { BaseAddress = url, Timeout = Timeout.InfiniteTimeSpan };
        using var events = await http.GetAsync(new Uri("/sindri/events", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, ct);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(ct));
        Assert.StartsWith("event: ", await reader.ReadLineAsync(ct), StringComparison.Ordinal);   // the stream is open

        using var stop = await http.PostAsync(new Uri("/sindri/stop", UriKind.Relative), content: null, ct);
        var rest = await reader.ReadToEndAsync(ct).WaitAsync(_prompt, ct);
        var code = await serve.WaitAsync(_prompt, ct);

        Assert.True(stop.IsSuccessStatusCode);
        Assert.NotNull(rest);
        Assert.Equal(0, code);
    }

    /// <summary>The endpoint serve recorded once it listens.</summary>
    private async Task<Uri> Endpoint(CancellationToken ct)
    {
        var file = new FileEndpoint(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml")), SliceHarness.Json);
        for (var waited = 0; ; waited += 50)
        {
            if ((await file.Read(ct)).Value is { } recorded)
            {
                return recorded.Url;
            }

            Assert.True(waited < 10_000, "serve didn't record its endpoint within 10 s");
            await Task.Delay(50, ct);
        }
    }
}
