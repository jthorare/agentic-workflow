using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3072: for every block the Add step menu offers (the API's own workflows-blocks golden), Add step then Save with nothing touched sends one add_node whose config is the block's example, unchanged.
/// #3944: except a field whose example names a step the workflow lacks ("{{triage.items}}"): it opens empty and is not sent; a required one keeps Save off until it is typed.
/// The engine side (that each example passes GRAPH-CONFIG-001) is BlockExamplesAcceptedTests in Aw.Sindri.Tests: the Ui project has no reference to the engine's validator.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class AddStepEveryKindTests
{
    private const string Typed = "typed by the person";

    // why: the roots the graph validator reads as scope, not as a step (GraphValidator.References in Aw.Sindri.Core)
    private static readonly string[] _scopeRoots = ["trigger", "item", "itemIndex", "workflow"];

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
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => b.Type == type);

        await page.GraphEdit.AddStep(block, Token);
        var steps = page.Editor.Nodes.Select(static n => n.Id).ToHashSet(StringComparer.Ordinal);
        var example = block.ExampleConfig is { } raw ? JsonNode.Parse(raw.GetRawText()) as JsonObject ?? [] : [];
        var expected = new JsonObject();
        foreach (var (name, value) in example)
        {
            if (!NamesMissingStep(value, steps))
            {
                expected[name] = value?.DeepClone();
                continue;
            }

            var field = page.Editor.Fields.Single(x => x.Name == name);
            Assert.True(field.Text.Length == 0, $"'{type}': field '{name}' opened with '{field.Text}', which names a step the workflow lacks");
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

    private static bool NamesMissingStep(JsonNode? value, HashSet<string> steps)
    {
        var text = value is JsonValue v && v.TryGetValue<string>(out var s) ? s : value?.ToJsonString() ?? string.Empty;
        return Reference.Matches(text).Select(static m => m.Groups["ref"].Value.Split('.', 2)[0]).Any(root => !_scopeRoots.Contains(root, StringComparer.Ordinal) && !steps.Contains(root));
    }

    [GeneratedRegex("\\{\\{\\s*(?<ref>[^{}]*?)\\s*\\}\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }
}
