using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The workflow.editor key (spec decision 14): sheet or steps, the sheet by default, an unknown value never throws.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowEditorKeyTests
{
    [Fact]
    public void The_key_is_named_workflow_editor() => Assert.Equal("workflow.editor", WorkflowEditorKey.Key);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("sheet", false)]
    [InlineData("steps", true)]
    [InlineData("STEPS", false)]
    [InlineData("canvas", false)]
    public void Reading_a_value_gives_its_layout_and_the_default_for_anything_else(string? value, bool steps) =>
        Assert.Equal(steps ? WorkflowEditorLayout.Steps : WorkflowEditorLayout.Sheet, WorkflowEditorKey.Read(value));

    [Theory]
    [InlineData("sheet", true)]
    [InlineData("steps", true)]
    [InlineData("canvas", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_sheet_and_steps_are_valid(string? value, bool valid) => Assert.Equal(valid, WorkflowEditorKey.IsValid(value));
}
