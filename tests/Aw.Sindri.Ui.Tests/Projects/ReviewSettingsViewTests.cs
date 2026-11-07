using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Review settings section in the project sheet: what a screen reader hears on its lines, buttons and fields, and that Reset, Save and the error lines are wired.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ReviewSettingsViewTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> Sheet(string scenario = "shared-entry")
    {
        Server.ReviewSettingsAnswer = RouteAnswer.Api("review-settings", scenario);
        var (window, shell) = await Connected();
        shell.Navigate(new DeepLink(DeepLinkKind.Project, "prj_0000000000000000000000000H"));
        await Until(window, () => shell.ProjectSheet.ReviewSettings.IsShown && Visible<Border>(window, "ReviewSettingsEntryLine") is not null, "the Review settings never showed");
        return (window, shell);
    }

    private static T? Visible<T>(Visual within, string name)
        where T : Control =>
        within.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible);

    private static List<T> All<T>(Visual within, string name)
        where T : Control =>
        [.. within.GetVisualDescendants().OfType<T>().Where(c => c.Name == name && c.IsEffectivelyVisible)];

    [Fact]
    public Task The_lines_of_the_section_read_as_one_text_each() => OnUi(async () =>
    {
        var (window, _) = await Sheet();

        Assert.Equal(Labels.ReviewSettingsEntryLine("sindri"), Heard.Wrapper(Visible<Border>(window, "ReviewSettingsEntryLine")!));
        Assert.Equal(Labels.ReviewSettingsSharedWith("prj_0000000000000000000000000K"), Heard.Wrapper(Visible<Border>(window, "ReviewSettingsShared")!));
        Assert.Equal(Labels.ReviewSettingsKeyLine(Labels.ReviewSettingsPanels, Labels.ReviewSettingsFromEntry), Heard.Wrapper(Visible<Border>(window, "ReviewSettingsPanelsHead")!));
        Assert.Equal(Labels.ReviewSettingsKeyLine(Labels.ReviewSettingsChecks, Labels.ReviewSettingsFromEntry), Heard.Wrapper(Visible<Border>(window, "ReviewSettingsChecksHead")!));
    });

    [Fact]
    public Task Every_button_and_field_has_a_name_that_says_which_row_it_is() => OnUi(async () =>
    {
        var (window, _) = await Sheet();

        Assert.Equal(Labels.ReviewSettingsReset, Heard.Name(Visible<Button>(window, "ReviewSettingsCollapseReset")!));
        Assert.Equal(Labels.ReviewSettingsSaveApprove, Heard.Name(Visible<Button>(window, "ReviewSettingsSave")!));
        Assert.Equal(Labels.ReviewSettingsMoveUp("Design doc map"), Heard.Name(All<Button>(window, "PanelUp")[1]));
        Assert.Equal(Labels.ReviewSettingsShowPanel("Test plan"), Heard.Name(All<CheckBox>(window, "PanelVisible")[5]));
        Assert.Equal(["Group 1, name", "Group 2, name", "Group 3, name", "Group 4, name"], All<TextBox>(window, "GroupName").Select(Heard.Name), StringComparer.Ordinal);
        Assert.Equal("Group 4, folded", Heard.Name(All<CheckBox>(window, "GroupFolded")[3]));
        Assert.Equal("Risky path 2, why it is risky", Heard.Name(All<TextBox>(window, "RiskWhy")[1]));
        Assert.Equal(["Check 1, id", "Check 2, id"], All<TextBox>(window, "CheckId").Select(Heard.Name), StringComparer.Ordinal);
        Assert.Equal("Remove Check 2", Heard.Name(All<Button>(window, "CheckRemove")[1]));
        Assert.Equal(Labels.ReviewSettingsNameField, Heard.Name(Visible<TextBox>(window, "ReviewSettingsName")!));
    });

    [Fact]
    public Task A_refused_field_shows_its_detail_as_one_text_under_the_key() => OnUi(async () =>
    {
        Server.ReviewSettingsSaveAnswer = RouteAnswer.Problem("review-settings-save", "bad-panels");
        var (window, shell) = await Sheet();

        await shell.ProjectSheet.ReviewSettings.Save(Token);
        await Until(window, () => Visible<Border>(window, "ReviewSettingsPanelsError") is not null, "the error never showed");

        Assert.Equal(shell.ProjectSheet.ReviewSettings.PanelsKey.Error, Heard.Wrapper(Visible<Border>(window, "ReviewSettingsPanelsError")!));
        Assert.Null(Visible<Border>(window, "ReviewSettingsCollapseError"));
    });

    [Fact]
    public Task A_reset_key_hides_its_editor_and_says_it_goes_back_to_default() => OnUi(async () =>
    {
        var (window, shell) = await Sheet();

        shell.ProjectSheet.ReviewSettings.Reset(shell.ProjectSheet.ReviewSettings.CollapseKey);
        await Until(window, () => Visible<Border>(window, "ReviewSettingsCollapseResetNote") is not null, "the note never showed");

        Assert.Equal(Labels.ReviewSettingsResetNote, Heard.Wrapper(Visible<Border>(window, "ReviewSettingsCollapseResetNote")!));
        Assert.Null(Visible<TextBox>(window, "ReviewSettingsCollapseText"));
    });

    [Fact]
    public Task A_read_only_section_disables_its_fields_and_hides_the_save() => OnUi(async () =>
    {
        Server.ReviewSettingsSaveAnswer = RouteAnswer.Problem("review-settings-save", "session-refused");
        var (window, shell) = await Sheet("session");

        await shell.ProjectSheet.ReviewSettings.Save(Token);
        await Until(window, () => Visible<Border>(window, "ReviewSettingsReadOnly") is not null, "the read-only line never showed");

        Assert.Equal(Labels.ReviewSettingsReadOnly, Heard.Wrapper(Visible<Border>(window, "ReviewSettingsReadOnly")!));
        Assert.Null(Visible<Button>(window, "ReviewSettingsSave"));
        Assert.False(Visible<TextBox>(window, "ReviewSettingsName")!.IsEnabled);
    });
}
