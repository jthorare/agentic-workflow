using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3072: for every block the Add step menu offers (the API's own workflows-blocks golden), Add step then Save with nothing touched sends one add_node whose config is the block's example, unchanged.
/// #3944: except a field whose example names a step the new one can't read ("{{triage.items}}"; nothing is selected here, so the new step follows no step and may read none):
/// it opens empty and is not sent; a required one keeps Save off until it is typed. Which fields those are comes from the Ui's ExampleReferences, tied to Core's copies by
/// ExampleReferencesCopyTests in Aw.Sindri.Tests.
/// #3999 (qa on #3944): no field of a new step, as the sheet opens, names a step, read with a hand scan of every "{{" rather than the editor's pattern.
/// The engine side (that each example passes GRAPH-CONFIG-001) is BlockExamplesAcceptedTests in Aw.Sindri.Tests: the Ui project has no reference to the engine's validator.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AddStepEveryKindTests
{
    private const string Typed = "typed by the person";

    private static readonly HashSet<string> _none = new(StringComparer.Ordinal);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> Kinds() =>
        [.. Golden("workflows-blocks", "listed")["groups"]!.AsArray().SelectMany(static g => g!["blocks"]!.AsArray()).Select(static b => b!["type"]!.GetValue<string>())];

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Save_with_the_example_untouched_sends_that_example_as_the_new_steps_settings(string type)
    {
        var f = new WorkflowsFixture();
        f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var page = await f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, type, StringComparison.Ordinal));

        await page.GraphEdit.AddStep(block, Token);
        var example = block.ExampleConfig is { } raw ? JsonNode.Parse(raw.GetRawText()) as JsonObject ?? [] : [];
        var expected = new JsonObject();
        foreach (var (name, value) in example)
        {
            if (!ExampleReferences.NamesStepNotUpstream(type, name, Text(value), _none))
            {
                expected[name] = value?.DeepClone();
                continue;
            }

            var field = page.Editor.Fields.Single(x => string.Equals(x.Name, name, StringComparison.Ordinal));
            Assert.Equal(string.Empty, field.Text);   // the example names a step the workflow lacks
            if (field.IsRequired)
            {
                Assert.False(page.CanSave, $"'{type}': Save is on with the required '{name}' empty");
                field.Text = Typed;
                expected[name] = Typed;
            }
        }

        Assert.True(page.CanSave, $"'{type}': Save is off with the example untouched; empty required: " + string.Join(", ", page.Editor.Fields.Where(static x => x.IsRequired && string.IsNullOrWhiteSpace(x.Text)).Select(static x => x.Name)));
        await page.Save(Token);

        var sent = JsonNode.Parse(Assert.Single(f.Server.AppliedWorkflowOps).Ops)!.AsArray()[0]!;
        Assert.Equal(type, sent["type"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expected, sent["config"] ?? new JsonObject()), $"'{type}': sent {sent["config"]} but expected {expected}");
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task No_prefilled_field_of_a_new_step_that_follows_nothing_names_a_step(string type)
    {
        var f = new WorkflowsFixture();
        f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var page = await f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, type, StringComparison.Ordinal));

        await page.GraphEdit.AddStep(block, Token);
        var foreign = page.Editor.Fields.SelectMany(static x => Roots(x.Text).Select(root => (x.Name, Root: root))).Where(static r => !ExampleReferences.ScopeRoots.Contains(r.Root)).ToList();

        Assert.True(foreign.Count == 0, $"'{type}': " + string.Join(", ", foreign.Select(static r => $"{r.Name} names '{r.Root}'")));
    }

    // Every root a "{{" opens, read by hand rather than with the pattern the editor uses, so a reference that pattern misses still fails here.
    private static IEnumerable<string> Roots(string text)
    {
        for (var at = text.IndexOf("{{", StringComparison.Ordinal); at >= 0; at = text.IndexOf("{{", at + 2, StringComparison.Ordinal))
        {
            var root = string.Concat(text[(at + 2)..].TrimStart().TakeWhile(static c => char.IsAsciiLetterOrDigit(c) || c == '_'));
            if (root.Length > 0)
            {
                yield return root;
            }
        }
    }

    private static string Text(JsonNode? value) => value is JsonValue v && v.TryGetValue<string>(out var s) ? s : value?.ToJsonString() ?? string.Empty;
}
