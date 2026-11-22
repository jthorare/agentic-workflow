using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3072: for every block the Add step menu offers (the API's own workflows-blocks golden), Add step then Save with nothing touched sends one add_node whose config is the block's example, unchanged.
/// #3944: except a field whose example names a step the workflow lacks ("{{triage.items}}"): it opens empty and is not sent; a required one keeps Save off until it is typed.
/// #3999 (qa on #3944): no field of a new step, as the sheet opens, names a step the workflow lacks, read with a hand scan of every "{{" rather than the editor's pattern.
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
    public async Task No_prefilled_field_of_a_new_step_names_a_step_the_workflow_lacks(string type)
    {
        var f = new WorkflowsFixture();
        f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var page = await f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, type, StringComparison.Ordinal));

        await page.GraphEdit.AddStep(block, Token);
        var known = page.Editor.Nodes.Select(static n => n.Id).Concat(_scopeRoots).ToHashSet(StringComparer.Ordinal);
        var foreign = page.Editor.Fields.SelectMany(static x => Roots(x.Text).Select(root => (x.Name, Root: root))).Where(r => !known.Contains(r.Root)).ToList();

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

    private static bool NamesMissingStep(JsonNode? value, HashSet<string> steps)
    {
        var text = value is JsonValue v && v.TryGetValue<string>(out var s) ? s : value?.ToJsonString() ?? string.Empty;
        return Reference.Matches(text).Select(static m => m.Groups["ref"].Value.Split('.', 2)[0]).Any(root => !_scopeRoots.Contains(root, StringComparer.Ordinal) && !steps.Contains(root));
    }

    [GeneratedRegex("\\{\\{\\s*(?<ref>[^{}]*?)\\s*\\}\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }
}
