using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features.PullSlack;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features;

/// <summary>Makes <see cref="SlackConnector"/>.</summary>
internal sealed class SlackConnectorFactory(TimeProvider clock) : ISlackConnectorFactory
{
    /// <inheritdoc />
    public IConnector Create(SlackSettings settings, ISlackReader reader, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(reader);
        var pull = new ValidatingService<PullSlackRequest, PullSlackResponse>(new PullSlackService(reader), new PullSlackValidator());
        return new SlackConnector(settings, reader, pull, window, clock);
    }
}
