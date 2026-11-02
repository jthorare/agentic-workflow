using Aw.Connectors.Core.Slack;
using Aw.Connectors.Infrastructure.Slack;
using Aw.Connectors.Testing.Slack;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Connectors.Tests.Slack;

public sealed class SlackClientContractTests : SlackReaderContract, IDisposable
{
    private const string Token = "xo" + "xb-contract-token-0123456789";

    private FakeSlackServer? _server;

    public void Dispose() => _server?.Dispose();

    protected override ISlackReader Reader(IReadOnlyList<string> allowed, IReadOnlyList<(string Channel, SlackMessage Message)> messages)
    {
        // The server knows more channels than the client may read, so a client that sent a request it should have refused would get an answer and fail the test.
        _server = new FakeSlackServer([.. allowed.Where(static id => id != DirectMessage), Other]);
        foreach (var (channel, message) in messages)
        {
            _ = _server.Add(channel, message);
        }

        return new SlackClient(
            new HttpClient(_server),
            new SlackClientSettings { Token = Token, Channels = allowed },
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero)));
    }
}
