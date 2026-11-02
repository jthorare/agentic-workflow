using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullSlack;

internal sealed class PullSlackValidator : IValidator<PullSlackRequest>
{
    private const int MaxLimit = 10_000;

    public Result<Unit> Validate(PullSlackRequest value)
    {
        if (value.Channels.Count == 0 || value.Channels.Any(static c => !SlackChannelIds.IsReadable(c.Channel)))
        {
            return ConnectorsErrors.Invalid.With("channels must be Slack channel ids ('C' or 'G' then capitals and digits); a direct message is never read.");
        }

        if (string.IsNullOrEmpty(value.Workspace))
        {
            return ConnectorsErrors.Invalid.With("workspace is required.");
        }

        return value.Limit is < 1 or > MaxLimit
            ? ConnectorsErrors.Invalid.With($"limit must be from 1 to {MaxLimit}.")
            : Unit.Value;
    }
}
