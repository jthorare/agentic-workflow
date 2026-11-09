using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Blocks;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IMeetingSource"/> honours: it names a preset, answers the meetings its app holds for the window with their fields whole,
/// and fills the transcript only when asked. It does not say which meetings fall in the window; the block filters the day.
/// </summary>
public abstract class MeetingSourceContract
{
    private static readonly DateTimeOffset _from = new(2026, 11, 9, 0, 0, 0, TimeSpan.FromHours(-8));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A source whose app holds exactly <paramref name="meetings"/>, each with its transcript.</summary>
    /// <param name="meetings">The meetings the app holds.</param>
    /// <returns>The source.</returns>
    protected abstract IMeetingSource Create(IReadOnlyList<Meeting> meetings);

    /// <summary>The source names the preset its connections come from.</summary>
    [Fact]
    public void The_source_names_a_preset() => Expect.True(!string.IsNullOrEmpty(Create([]).Preset));

    /// <summary>A meeting reads back with every field.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_meeting_reads_back_with_every_field()
    {
        var meeting = new Meeting("planning", _from.AddHours(9), _from.AddHours(10), ["ana", "bo"], "notes", "transcript", "https://notes.example/m/1", "granola");
        var source = Create([meeting]);

        var read = await source.Read(ConnectionName.Parse($"{source.Preset}:work").Value, _from, _from.AddDays(1), withTranscript: true, Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(meeting, Expect.Only([.. read.Value]));
    }

    /// <summary>The transcript is filled only when asked for.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_transcript_is_filled_only_when_asked_for()
    {
        var source = Create([new Meeting("planning", _from.AddHours(9), _from.AddHours(10), ["ana"], "notes", "transcript", "https://notes.example/m/1", "granola")]);
        var name = ConnectionName.Parse($"{source.Preset}:work").Value;

        var without = await source.Read(name, _from, _from.AddDays(1), withTranscript: false, Token);

        Expect.True(without.IsSuccess);
        Expect.True(Expect.Only([.. without.Value]).Transcript is null);
    }
}
