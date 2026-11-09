using Aw.Sindri.Infrastructure.Handoff;
using Aw.Sindri.Testing.Handoff;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>The two implementations of the inbox contract, each over its own fake inbox (page size 2, key 'inbox-key').</summary>
internal static class InboxPairs
{
    internal const string Key = "inbox-key";

    internal static InboxUnderTest Fake()
    {
        var backing = new FakeInbox(Key, pageSize: 2);
        return new InboxUnderTest(backing, backing, new Uri("http://127.0.0.1:1/"), owned: null);
    }

    internal static InboxUnderTest Http() => Http(out _);

    internal static InboxUnderTest Http(out FakeInboxServer server)
    {
        var backing = new FakeInbox(Key, pageSize: 2);
        server = new FakeInboxServer(backing);
        var client = new HttpInboxClient(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }), ["127.0.0.1"], TimeSpan.Zero);
        return new InboxUnderTest(client, backing, server.Url, server);
    }
}
