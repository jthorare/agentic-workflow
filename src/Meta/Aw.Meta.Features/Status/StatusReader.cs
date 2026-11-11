using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Status;

/// <summary>Reads the parts of the status.</summary>
/// <param name="reader">The open obligations.</param>
/// <param name="queue">The merge queue.</param>
/// <param name="owner">The owner's queue.</param>
/// <param name="lanes">The lane map.</param>
/// <param name="contexts">The lanes' context windows.</param>
internal sealed class StatusReader(ObligationReader reader, MergeQueueReader queue, IOwnerStore owner, ILaneStore lanes, LaneContextReader contexts)
{
    internal ValueTask<IReadOnlyList<LaneContext>?> Context(TeamConfig team, CancellationToken cancellationToken) => contexts.ReadOrNull(team, cancellationToken);

    internal async ValueTask<Result<StatusResponse>> Read(string metaLane, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var open = await reader.Open(metaLane, now, cancellationToken).ConfigureAwait(false);
        var questions = await owner.List(includeAnswered: false, cancellationToken).ConfigureAwait(false);
        var map = await lanes.Current(cancellationToken).ConfigureAwait(false);
        var ready = await queue.Read(cancellationToken, withGithub: true).ConfigureAwait(false);
        if (open.IsError || questions.IsError || map.IsError || ready.IsError)
        {
            return open.IsError ? open.Error : questions.IsError ? questions.Error : map.IsError ? map.Error : ready.Error;
        }

        return new StatusResponse(open.Value, ready.Value, questions.Value, map.Value);
    }
}
