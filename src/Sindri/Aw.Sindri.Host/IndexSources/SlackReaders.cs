using Aw.Connectors.Core.Slack;
using Aw.Connectors.Infrastructure;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>Makes the Slack reader the feed and 'index connect slack' read with: the Connectors package's own, on a handler that follows no redirect, talking only to slack.com and refusing any channel id it was not built for.</summary>
internal static class SlackReaders
{
    internal static ISlackReader Create(string token, IReadOnlyCollection<string> channels) =>
        new ServiceCollection().AddSlackReader(token, channels).BuildServiceProvider().GetRequiredService<ISlackReader>();
}
