using System.Globalization;
using System.Net;
using Aw.Connectors.Testing.Server;
using Aw.Connectors.Testing.Slack;

// The fake Slack on loopback (messages spec §8.1): `Aw.Connectors.Testing.Server [--port N] [--empty]`. It seeds SlackWorkspaceSeed unless --empty, prints
// one line, "listening http://127.0.0.1:<port>/", and serves until Ctrl+C or SIGTERM. Exit 0 stopped, 2 usage.
var port = 0;
var seed = true;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--empty")
    {
        seed = false;
    }
    else if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var chosen) && chosen <= IPEndPoint.MaxPort)
    {
        port = chosen;
        i++;
    }
    else
    {
        await Console.Error.WriteLineAsync("usage: Aw.Connectors.Testing.Server [--port N] [--empty]");
        return 2;
    }
}

var server = new FakeSlackServer();
if (seed)
{
    _ = server.SeedWorkspace();
}

await using var host = await FakeSlackHost.Start(server, port, CancellationToken.None);
await Console.Out.WriteLineAsync("listening " + host.BaseUrl.AbsoluteUri);
await Console.Out.FlushAsync(CancellationToken.None);
await host.WaitForShutdown(CancellationToken.None);
return 0;
