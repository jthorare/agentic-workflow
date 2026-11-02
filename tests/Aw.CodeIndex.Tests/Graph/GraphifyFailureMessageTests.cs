using Aw.CodeIndex.Infrastructure.Graph;

namespace Aw.CodeIndex.Tests.Graph;

/// <summary>What the graph layer's detail says when graphify ends non-zero: the exit or the signal, and the end of its stderr (#1680).</summary>
[Trait("Lane", "unit")]
public sealed class GraphifyFailureMessageTests
{
    private static string Identity(string text) => text;

    [Theory]
    [InlineData(3, "graphify exited with code 3")]
    [InlineData(128, "graphify exited with code 128")]
    [InlineData(129, "graphify was killed by signal 1 (exit 129)")]
    [InlineData(137, "graphify was killed by signal 9 (exit 137)")]
    [InlineData(159, "graphify was killed by signal 31 (exit 159)")]
    [InlineData(160, "graphify exited with code 160")]
    public void The_signal_range_is_128_plus_N(int exit, string expected) =>
        Assert.Equal(expected, GraphifyFailure.Message(exit, string.Empty, Identity));

    [Fact]
    public void The_message_ends_with_the_last_five_non_empty_lines_of_stderr()
    {
        var message = GraphifyFailure.Message(3, "one\n\ntwo\nthree\nfour\nfive\nsix\n", Identity);

        Assert.Equal("graphify exited with code 3: two | three | four | five | six", message);
    }

    [Fact]
    public void The_tail_is_cut_to_its_last_500_characters_and_scrubbed()
    {
        var tail = GraphifyFailure.Tail(new string('a', 2000) + "XYZ", static text => text.Replace("XYZ", "[REDACTED]", StringComparison.Ordinal));

        Assert.Equal(GraphifyFailure.MaxCharacters, tail.Length);
        Assert.EndsWith("[REDACTED]", tail, StringComparison.Ordinal);
    }
}
