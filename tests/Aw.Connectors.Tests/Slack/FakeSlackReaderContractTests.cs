using Aw.Connectors.Core.Slack;
using Aw.Connectors.Testing.Slack;

namespace Aw.Connectors.Tests.Slack;

public sealed class FakeSlackReaderContractTests : SlackReaderContract
{
    protected override ISlackReader Reader(IReadOnlyList<string> allowed, IReadOnlyList<(string Channel, SlackMessage Message)> messages)
    {
        var fake = new FakeSlackReader([.. allowed]);
        foreach (var (channel, message) in messages)
        {
            _ = fake.Add(channel, message);
        }

        return fake;
    }
}
