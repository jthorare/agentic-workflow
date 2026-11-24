using Avalonia.Controls;
using Avalonia.Headless;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Icons;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3999: the refusal line under the graph edit bar and the one in the step sheet read as errors, as the Queue page's problem line does: the error icon
/// (StateIcon Down, class error) beside the text in the error tone (TextBlock class error, Brush.Error).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowsFailureLineTests
{
    private static Task<(bool Icon, bool Text)> ErrorTone(Func<UserControl> make, string icon, string text) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WorkflowsFailureLineTests).Assembly).Dispatch(
            () =>
            {
                var view = make();
                var glyph = view.FindControl<StateIcon>(icon);
                var line = view.FindControl<TextBlock>(text);
                return (glyph is not null && glyph.Classes.Contains("error", StringComparer.Ordinal), line is not null && line.Classes.Contains("error", StringComparer.Ordinal));
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_page_line_under_the_graph_edit_bar_has_the_error_icon_and_tone()
    {
        var (icon, text) = await ErrorTone(static () => new GraphEditBar(), "EditFailureIcon", "EditFailureText");

        Assert.True(icon, "the page's refusal line has no error icon");
        Assert.True(text, "the page's refusal line is not in the error tone");
    }

    [Fact]
    public async Task The_sheet_line_has_the_error_icon_and_tone()
    {
        var (icon, text) = await ErrorTone(static () => new WorkflowEditorSheet(), "EditorFailureIcon", "EditorFailureText");

        Assert.True(icon, "the sheet's refusal line has no error icon");
        Assert.True(text, "the sheet's refusal line is not in the error tone");
    }
}
