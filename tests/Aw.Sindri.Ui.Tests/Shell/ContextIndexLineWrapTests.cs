using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// A Context index line that wraps at text scale 1.3 (#4269): the label stands in its own column so a wrapped line hangs past it, and the rest
/// keeps an error code and a time phrase whole, so "(SND-" and "ago" never start a line that reads as a row of its own.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ContextIndexLineWrapTests
{
    private const char Nbsp = ' ';
    private const char NbHyphen = '‑';

    [Fact]
    public void A_source_line_splits_into_its_label_and_a_rest_whose_age_is_one_phrase()
    {
        var line = new ContextIndexLine("linear: first sync, 0 records so far · last commit 1 min ago");

        Assert.Equal("linear:", line.Label, StringComparer.Ordinal);
        Assert.Equal($"first sync, 0 records so far · last commit 1{Nbsp}min{Nbsp}ago", line.Rest, StringComparer.Ordinal);
        Assert.True(line.HasLabel);
    }

    [Fact]
    public void The_concepts_line_keeps_its_error_code_whole()
    {
        var line = new ContextIndexLine("Concepts: none yet · last run 2 h ago (SND-CONTEXT-030)");

        Assert.Equal("Concepts:", line.Label, StringComparer.Ordinal);
        Assert.Equal($"none yet · last run 2{Nbsp}h{Nbsp}ago (SND{NbHyphen}CONTEXT{NbHyphen}030)", line.Rest, StringComparer.Ordinal);
    }

    [Fact]
    public void Nothing_committed_yet_and_under_a_minute_ago_stay_together()
    {
        Assert.Equal($"idle · 0 records · nothing{Nbsp}committed{Nbsp}yet", new ContextIndexLine("linear-projects: idle · 0 records · nothing committed yet").Rest, StringComparer.Ordinal);
        Assert.Equal($"idle · last commit under{Nbsp}a{Nbsp}minute{Nbsp}ago", new ContextIndexLine("library: idle · last commit under a minute ago").Rest, StringComparer.Ordinal);
    }

    [Fact]
    public void A_line_with_no_label_is_all_rest_and_its_words_and_name_stay_plain()
    {
        var line = new ContextIndexLine("Concepts are off");

        Assert.Equal(string.Empty, line.Label, StringComparer.Ordinal);
        Assert.False(line.HasLabel);
        Assert.Equal("Concepts are off", line.Rest, StringComparer.Ordinal);
        Assert.Equal("Concepts are off", line.Text, StringComparer.Ordinal);
        Assert.EndsWith("Concepts are off", line.Name, StringComparison.Ordinal);
    }
}
