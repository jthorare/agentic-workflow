using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#2807 items 2 and 4: the step editor names the step and its kind (never the node id or the revision), each field shows its caption (never the config key), cron and time zone offer presets with the raw text under Advanced, and a selected step shows its title and summary under the graph. Served from the API's own goldens (workflows-blocks, workflows-slug), the first node made a schedule trigger.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsStepEditorFramesTests : FrameHost
{
    private const string Title = "Every weekday at 7:00";
    private const string Summary = "Every weekday at 7:00 (America/Los_Angeles)";
    private const string OddZone = "Mars/Olympus_Mons";

    // The found golden with its first node ("start") turned into a schedule trigger, and the summaries the API serves for it.
    private static string ScheduleWorkflow(string zone, bool archived = false)
    {
        var workflow = Golden("workflows-slug", "found");
        var node = workflow["spec"]!["nodes"]![0]!;
        node["type"] = "schedule";
        node["title"] = Title;
        node["config"] = new JsonObject { ["cron"] = "0 7 * * 1-5", ["timezone"] = zone };
        workflow["summaries"]![0]!["summary"] = Summary;
        if (archived)
        {
            workflow["archivedAt"] = "2026-01-01T00:00:00.0000000Z";
        }

        return workflow.ToJsonString();
    }

    private async Task<(Window Window, ShellViewModel Shell)> OnTheTriggerEditor(string zone = "America/Los_Angeles", bool archived = false)
    {
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, ScheduleWorkflow(zone, archived));
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");
        return (window, shell);
    }

    private static async Task OpenEditorOnTrigger(Window window, ShellViewModel shell)
    {
        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("start"));
        await shell.Workflows.OpenSettings(CancellationToken.None);
        await Until(window, () => shell.Workflows.Editor.Fields.Count > 0 && Visible(window, "EditorFields"), "the editor never showed the trigger's fields");
    }

    private static IEnumerable<string> SheetTexts(Window window) =>
        Named<Control>(window, "EditorSheet").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text!);

    [Fact]
    public Task The_editor_header_names_the_step_and_its_kind_and_never_the_node_id_or_the_revision() => OnUi(async () =>
    {
        var (window, shell) = await OnTheTriggerEditor();
        await OpenEditorOnTrigger(window, shell);

        Assert.Equal($"{Title} · trigger", AutomationProperties.GetName(Named<Border>(window, "EditorHeadingName")));
        var texts = SheetTexts(window).ToList();
        Assert.Contains(texts, t => string.Equals(t, $"{Title} \u00b7 trigger", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("revision", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(texts, t => t.Contains("Settings of", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("start", StringComparison.OrdinalIgnoreCase));   // the node id
    });

    [Fact]
    public Task Each_field_shows_its_caption_and_never_its_config_key() => OnUi(async () =>
    {
        var (window, shell) = await OnTheTriggerEditor();
        await OpenEditorOnTrigger(window, shell);

        var texts = SheetTexts(window).ToList();
        Assert.Contains(texts, t => t.StartsWith("Runs at", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("Time zone", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.StartsWith("cron", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.StartsWith("timezone", StringComparison.Ordinal));
        Assert.Equal(["Runs at (required)", "Time zone"], shell.Workflows.Editor.Fields.Select(f => f.Caption), StringComparer.Ordinal);
    });

    [Fact]
    public Task Cron_offers_its_presets_and_keeps_the_raw_cron_under_Advanced_while_the_time_zone_shows_its_text() => OnUi(async () =>
    {
        var (window, shell) = await OnTheTriggerEditor();
        await OpenEditorOnTrigger(window, shell);
        Settle(window);

        var fields = Named<ItemsControl>(window, "EditorFields");
        var presets = fields.GetVisualDescendants().OfType<ComboBox>().Where(c => c.Name == "EditorFieldPresets" && c.IsEffectivelyVisible).ToList();
        Assert.Equal(2, presets.Count);   // cron and time zone
        var cron = presets[0];
        Assert.Equal(4, cron.ItemCount);
        Assert.Contains(cron.Items.OfType<object>(), item => item.ToString()!.Contains("Every weekday at 7:00", StringComparison.Ordinal));   // a choice prints its label
        Assert.Equal("0 7 * * 1-5", shell.Workflows.Editor.Fields[0].Text);

        // the raw cron is hidden until Advanced is pressed
        var raw = fields.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "EditorFieldText" && t.DataContext == shell.Workflows.Editor.Fields[0]);
        Assert.False(raw.IsEffectivelyVisible, "the raw cron shows before Advanced is pressed");
        var advanced = fields.GetVisualDescendants().OfType<ToggleButton>().First(b => b.Name == "EditorFieldAdvanced" && b.DataContext == shell.Workflows.Editor.Fields[0]);
        Assert.Equal("Advanced", advanced.Content);
        advanced.IsChecked = true;
        await Until(window, () => raw.IsEffectivelyVisible, "Advanced never showed the raw cron");
        Assert.Equal("0 7 * * 1-5", raw.Text);

        // a time zone's preset labels equal their values, so there is nothing to hide: its text sits next to the preset list, with no Advanced toggle
        var zone = shell.Workflows.Editor.Fields[1];
        Assert.True(fields.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "EditorFieldText" && t.DataContext == zone).IsEffectivelyVisible, "the time zone's text is hidden");
        Assert.DoesNotContain(fields.GetVisualDescendants().OfType<ToggleButton>(), b => b.Name == "EditorFieldAdvanced" && b.DataContext == zone && b.IsEffectivelyVisible);
    });

    [Fact]
    public Task Picking_a_preset_sets_the_field_and_the_time_zone_still_takes_free_text() => OnUi(async () =>
    {
        var (window, shell) = await OnTheTriggerEditor(OddZone);
        await OpenEditorOnTrigger(window, shell);
        Settle(window);
        var (cron, zone) = (shell.Workflows.Editor.Fields[0], shell.Workflows.Editor.Fields[1]);

        var fields = Named<ItemsControl>(window, "EditorFields");
        var cronPresets = fields.GetVisualDescendants().OfType<ComboBox>().First(c => c.Name == "EditorFieldPresets" && c.DataContext == cron);
        cronPresets.SelectedIndex = 1;   // Every day at 9:00
        await Until(window, () => string.Equals(cron.Text, "0 9 * * *", StringComparison.Ordinal), "the preset never reached the cron field");

        // a zone that is no preset is kept and shown as text, so it is never lost by opening the editor
        Assert.Equal(OddZone, zone.Text);
        var zoneText = fields.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "EditorFieldText" && t.DataContext == zone);
        Assert.True(zoneText.IsEffectivelyVisible, "a time zone outside the presets is not shown as text");
        Assert.Equal(OddZone, zoneText.Text);
    });

    [Fact]
    public Task A_selected_step_shows_its_title_and_summary_under_the_graph_until_Edit_is_on() => OnUi(async () =>
    {
        var (window, shell) = await OnTheTriggerEditor();
        Assert.False(Visible(window, "StepSummary"), "a step summary shows with no step selected");

        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("start"));
        await Until(window, () => Visible(window, "StepSummary"), "selecting a step never showed its summary");
        var line = AutomationProperties.GetName(Named<Border>(window, "StepSummary"));
        Assert.Contains(Title, line, StringComparison.Ordinal);
        Assert.Contains(Summary, line, StringComparison.Ordinal);

        await shell.Workflows.OpenSettings(CancellationToken.None);
        await Until(window, () => !Visible(window, "StepSummary"), "the summary stayed while step settings are open");
    });

    [Fact]
    public Task An_archived_workflows_selected_step_still_shows_its_summary_but_no_Step_settings() => OnUi(async () =>
    {
        var (window, shell) = await OnTheTriggerEditor(archived: true);

        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("start"));
        await Until(window, () => Visible(window, "StepSummary"), "an archived workflow's selected step never showed its summary");

        Assert.False(Visible(window, "StepSettings"), "an archived workflow offers Step settings");
    });

    [Fact]
    public Task A_boolean_field_in_the_step_editor_draws_its_caption_once() => OnUi(async () =>
    {
        var workflow = Golden("workflows-slug", "found");
        var node = workflow["spec"]!["nodes"]![0]!;
        node["type"] = "run_workflow";
        node["config"] = new JsonObject { ["wait"] = true };
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");
        await OpenEditorOnTrigger(window, shell);
        Settle(window);

        var field = shell.Workflows.Editor.Fields.Single(f => f.IsBoolean);
        var fields = Named<ItemsControl>(window, "EditorFields");
        var check = fields.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "EditorFieldCheck" && c.DataContext == field);
        Assert.Equal(field.Caption, AutomationProperties.GetName(check));
        var row = check.GetVisualAncestors().OfType<StackPanel>().First(p => p.DataContext == field);
        var drawn = row.GetVisualDescendants().OfType<TextBlock>().Count(t => t.IsEffectivelyVisible && string.Equals(t.Text, field.Caption, StringComparison.Ordinal));
        Assert.Equal(1, drawn);   // the caption line above the box, not the box's own content too
    });
}
