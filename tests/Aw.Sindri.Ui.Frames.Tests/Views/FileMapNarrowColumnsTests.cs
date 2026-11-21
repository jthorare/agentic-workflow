using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Files view at 1000 wide (#3739): a row's status words stay inside the File column, and the column headings cut short with an ellipsis instead of clipping.</summary>
[Lane(TestLane.Integration)]
public sealed class FileMapNarrowColumnsTests : FrameHost
{
    // The Files grid is 2*,3*,2*,2* with an 8 px margin on each side.
    private const double FileColumnShare = 2.0 / 9.0;
    private const double Margin = 8;

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OpenFiles()
    {
        var (window, shell) = await Connected(width: 1000, height: 900);
        shell.GoTo(PageId.Context);
        Settle(window);
        await shell.Context.FileMap.Load("src/tabs/", Token);
        shell.Context.ChooseView(files: true);
        await Until(window, () => Visible(window, "FileRows"), "the file rows never drew: " + shell.Context.FileMap.Problem);
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task A_suggested_rows_status_words_end_inside_the_file_column_at_1000() => OnUi(async () =>
    {
        var (window, _) = await OpenFiles();

        var status = window.GetVisualDescendants().OfType<TextBlock>().First(t => string.Equals(t.Text, Labels.ContextFilesSuggested, StringComparison.Ordinal));
        var row = status.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("file-row"));
        var columnRight = Margin + ((row.Bounds.Width - (2 * Margin)) * FileColumnShare);
        var right = status.TranslatePoint(default, row)!.Value.X + status.Bounds.Width;

        Assert.True(right <= columnRight + 0.5, $"the status ends at {right:F1} px, the File column ends at {columnRight:F1} px");
    });

    [Fact]
    public Task The_symbols_heading_trims_with_an_ellipsis_instead_of_clipping() => OnUi(async () =>
    {
        var (window, shell) = await OpenFiles();

        var heading = window.GetVisualDescendants().OfType<TextBlock>().First(t => string.Equals(t.Text, shell.Context.FileMap.SymbolsHeading, StringComparison.Ordinal));

        Assert.NotEqual(Avalonia.Media.TextTrimming.None, heading.TextTrimming);
    });
}
