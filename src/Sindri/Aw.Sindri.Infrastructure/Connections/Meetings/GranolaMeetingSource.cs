using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Connections.Meetings;

/// <summary>The Granola note app behind <see cref="IMeetingSource"/>. A stub: the mapping is written after scripts/probe-meeting-apps.sh shows whether Granola exposes MCP tools or REST.</summary>
internal sealed class GranolaMeetingSource : IMeetingSource
{
    public string Preset => "granola";

    public ValueTask<Result<IReadOnlyList<Meeting>>> Read(ConnectionName connection, DateTimeOffset from, DateTimeOffset to, bool withTranscript, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<Meeting>>>(SindriErrors.GraphBlockFailed.With("meetings: the Granola mapping is not built until scripts/probe-meeting-apps.sh has run with a real account"));
}
