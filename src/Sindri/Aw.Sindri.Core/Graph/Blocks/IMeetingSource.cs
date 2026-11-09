using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'meetings' node's meetings come from: one note app behind one connection preset ('granola', 'wispr-flow'). A third app is one more class.</summary>
public interface IMeetingSource
{
    /// <summary>The connection preset (and so the connection name's service part) this source reads, such as 'granola'.</summary>
    public string Preset { get; }

    /// <summary>Reads the meetings that started inside a window.</summary>
    /// <param name="connection">The signed-in connection to read with.</param>
    /// <param name="from">The window's start, inclusive.</param>
    /// <param name="to">The window's end, exclusive.</param>
    /// <param name="withTranscript">True to fill <see cref="Meeting.Transcript"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The meetings, or why they could not be read.</returns>
    public ValueTask<Result<IReadOnlyList<Meeting>>> Read(ConnectionName connection, DateTimeOffset from, DateTimeOffset to, bool withTranscript, CancellationToken cancellationToken);
}
