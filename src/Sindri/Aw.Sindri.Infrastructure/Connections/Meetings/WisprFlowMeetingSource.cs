using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Connections.Meetings;

/// <summary>The Wispr Flow note app behind <see cref="IMeetingSource"/>. A stub: the mapping is written after scripts/probe-meeting-apps.sh shows whether Wispr Flow exposes MCP tools or REST.</summary>
internal sealed class WisprFlowMeetingSource : IMeetingSource
{
    public string Preset => "wispr-flow";

    public ValueTask<Result<IReadOnlyList<Meeting>>> Read(ConnectionName connection, DateTimeOffset from, DateTimeOffset to, bool withTranscript, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<Meeting>>>(SindriErrors.GraphBlockFailed.With("meetings: the Wispr Flow mapping is not built until scripts/probe-meeting-apps.sh has run with a real account"));
}
