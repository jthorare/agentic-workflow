using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Work table's one-line row (F3 spec work §1 and §8 slice 1): the id beside the title, the status word, and the stage in plain words with "Not started" when there is none.</summary>
[Lane(TestLane.Unit)]
public sealed class QueueTableRowsTests
{
    private static ItemRowViewModel Row(string? step, string state = "running") =>
        new(new ItemSummaryView("ITM-01JN2", "Try the queue", "agentic-workflow", state, step, A.Noon.AddMinutes(-12), PrimaryAction: null, Actions: []), "running", primaryAction: null, A.Noon);

    [Theory]
    [InlineData("scoping", "Scoping")]
    [InlineData("design", "Design")]
    [InlineData("plan", "Plan")]
    [InlineData("phase", "Build and prove")]
    [InlineData("merge", "Merge")]
    public void The_lines_five_stages_read_in_plain_words(string step, string expected)
    {
        var row = Row(step);

        Assert.Equal((expected, false), (row.Stage, row.StageIsRaw));
    }

    [Fact]
    public void No_step_reads_not_started()
    {
        var row = Row(step: null);

        Assert.Equal((Labels.NotStarted, false), (row.Stage, row.StageIsRaw));
    }

    [Fact]
    public void Any_other_step_shows_as_served_and_is_marked_raw()
    {
        var row = Row("triaged");

        Assert.Equal(("triaged", true), (row.Stage, row.StageIsRaw));
    }

    [Fact]
    public void The_id_sits_beside_the_title_and_the_status_is_a_word()
    {
        var row = Row("plan");

        Assert.Equal(("ITM-01JN2", "Try the queue", "Running"), (row.Id, row.Title, row.StateWord));
    }

    [Fact]
    public void The_column_headings_are_sentence_case() =>
        Assert.Equal(["Title", "Repo", "Status", "Stage", "Age"], [Labels.ColumnTitle, Labels.ColumnRepo, Labels.ColumnState, Labels.ColumnStep, Labels.ColumnSince], StringComparer.Ordinal);
}
