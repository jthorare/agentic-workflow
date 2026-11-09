using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Testing;

/// <summary>A note app with a controllable list of meetings. It ignores the window, as an app that returns more than asked would, so the block's own day filter is what a test sees.</summary>
/// <param name="preset">The connection preset it stands for.</param>
public sealed class FakeMeetingSource(string preset = "granola") : IMeetingSource
{
    /// <summary>Gets the meetings the app has.</summary>
    public IList<Meeting> Meetings { get; } = [];

    /// <summary>Gets the reads made: the connection, the window and whether the transcript was asked for.</summary>
    public IList<(string Connection, DateTimeOffset From, DateTimeOffset To, bool WithTranscript)> Reads { get; } = [];

    /// <summary>Gets or sets the error the next reads answer with; null reads normally.</summary>
    public Error? Fail { get; set; }

    /// <inheritdoc/>
    public string Preset => preset;

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<Meeting>>> Read(ConnectionName connection, DateTimeOffset from, DateTimeOffset to, bool withTranscript, CancellationToken cancellationToken)
    {
        Reads.Add((connection.ToString(), from, to, withTranscript));
        if (Fail is { } error)
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<Meeting>>(error));
        }

        IReadOnlyList<Meeting> copy = [.. Meetings.Select(m => withTranscript ? m : m with { Transcript = null })];
        return ValueTask.FromResult(Results.Success(copy));
    }
}
