using Aw.Connectors.Testing.Server;
using Aw.Connectors.Testing.Slack;

// The fake Slack on loopback (messages spec §8.1): see FakeSlackHostOptions.Usage. It seeds SlackWorkspaceSeed unless --empty, queues the
// --rate-limit 429s and adds the --gap messages, prints one line, "listening http://127.0.0.1:<port>/", and serves until Ctrl+C or SIGTERM.
// Exit 0 stopped, 2 usage.
var (options, error) = FakeSlackHostOptions.Parse(args);
if (options is null)
{
    await Console.Error.WriteLineAsync(error + "\n" + FakeSlackHostOptions.Usage);
    return 2;
}

var server = options.Apply(new FakeSlackServer());
await using var host = await FakeSlackHost.Start(server, options.Port, CancellationToken.None);
await Console.Out.WriteLineAsync("listening " + host.BaseUrl.AbsoluteUri);
await Console.Out.FlushAsync(CancellationToken.None);
await host.WaitForShutdown(CancellationToken.None);
return 0;
