using System.Globalization;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Transcripts;

namespace Aw.Judge.Tests.Transcripts;

public sealed class TurnOutcomesTests
{
    private static readonly SessionId _session = SessionId.From("s");

    [Theory, Trait("Lane", "unit")]
    [InlineData("fix the bug", true)]
    [InlineData("  hello", true)]
    [InlineData("<system-reminder>x</system-reminder>", false)]
    [InlineData("  <command-name>/clear</command-name>", false)]
    [InlineData("This session is being continued from a previous conversation that ran out", false)]
    public void IsRealPromptText_rejects_machine_text(string text, bool expected) =>
        Assert.Equal(expected, TurnOutcomes.IsRealPromptText(text));

    [Fact, Trait("Lane", "unit")]
    public void TurnDiff_marks_removed_and_added_lines_under_a_file_header()
    {
        var diff = TurnOutcomes.TurnDiff([new TranscriptEdit("a.cs", "one\ntwo", "three"), new TranscriptEdit("b.cs", "", "new file")]);

        Assert.Equal("--- a.cs\n- one\n- two\n+ three\n--- b.cs\n+ new file", diff);
    }

    [Fact, Trait("Lane", "unit")]
    public void Survival_counts_lines_a_later_edit_of_the_same_file_removed()
    {
        var session = Turns(
            [new TranscriptEdit("a.cs", "", "int a = 1;\nint b = 2;\nint c = 3;\nint d = 4;")],
            [new TranscriptEdit("a.cs", "int a = 1;\nint b = 2;", "int e = 5;")],
            [new TranscriptEdit("other.cs", "int c = 3;", "")]);

        Assert.Equal(0.5, TurnOutcomes.Survival(session, index: 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void Survival_is_unknown_when_a_turn_added_only_trivial_lines()
    {
        var session = Turns([new TranscriptEdit("a.cs", "", "{\n\n}")]);

        Assert.Null(TurnOutcomes.Survival(session, index: 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void TurnLabel_is_stalled_when_most_lines_were_removed_even_without_a_correction()
    {
        var session = Turns(
            [new TranscriptEdit("a.cs", "", "int a = 1;\nint b = 2;\nint c = 3;")],
            [new TranscriptEdit("a.cs", "int a = 1;\nint b = 2;\nint c = 3;", "int z = 0;")]);

        Assert.Equal(TurnOutcomes.Stalled, TurnOutcomes.TurnLabel(session, index: 0, sessionClosed: false));
    }

    [Fact, Trait("Lane", "unit")]
    public void TurnLabel_has_no_signal_between_the_survival_thresholds_or_before_the_session_closes()
    {
        var middle = Turns(
            [new TranscriptEdit("a.cs", "", "int a = 1;\nint b = 2;")],
            [new TranscriptEdit("a.cs", "int a = 1;", "int z = 0;")]);
        var last = Turns([new TranscriptEdit("a.cs", "", "int a = 1;")]);

        Assert.Null(TurnOutcomes.TurnLabel(middle, index: 0, sessionClosed: true));
        Assert.Null(TurnOutcomes.TurnLabel(last, index: 0, sessionClosed: false));
        Assert.Equal(TurnOutcomes.Progressing, TurnOutcomes.TurnLabel(last, index: 0, sessionClosed: true));
    }

    private static List<TranscriptTurn> Turns(params TranscriptEdit[][] edits) =>
        [.. edits.Select((e, i) => new TranscriptTurn(_session, i, string.Create(CultureInfo.InvariantCulture, $"prompt {i}"), e))];
}
