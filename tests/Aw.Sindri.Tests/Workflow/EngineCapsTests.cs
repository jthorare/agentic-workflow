using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>Owner q93, 2026-10-23: the engine's size and count caps sit far above any real run; a value past each old cap is accepted.</summary>
[Lane(TestLane.Unit)]
public sealed class EngineCapsTests
{
    [Fact]
    public void A_recipe_file_past_the_old_256_KiB_is_read()
    {
        var text = RecipeReaderTests.Minimal + "description: \"" + new string('x', (256 * 1024) + 10) + "\"\n";

        var reading = RecipeReaderTests.Reader.Read(text, "big.yaml");

        Assert.DoesNotContain(reading.Findings, static f => f.Rule == "RCP-SCHEMA-003");
    }

    [Fact]
    public void A_diff_past_the_old_2_MiB_is_read_whole()
    {
        var text = new string('d', (2 * 1024 * 1024) + 10);

        var (kept, truncated) = ChangeRead.Scoping.Cut(text);

        Assert.False(truncated);
        Assert.Equal(text.Length, kept.Length);
    }

    [Fact]
    public async Task More_sibling_recipes_than_the_old_64_resolve()
    {
        const int Count = 80;
        var files = new InMemoryRecipeFiles();
        for (var i = 1; i <= Count; i++)
        {
            _ = files.With(Path(i), i < Count ? Parent(i, Id(i + 1)) : Leaf(i));
        }

        var head = RecipeReaderTests.Reader.Read(Parent(0, "workflow.sib-1"), "head.yaml").Document!;
        var resolver = await SameDirectoryChildren.Load("/r/head.yaml", head.Recipe, RecipeReaderTests.Reader, files, TestContext.Current.CancellationToken);

        Assert.NotNull(resolver.Resolve(RecipeId.From(Id(Count))));
    }

    private static string Id(int n) => "workflow.sib-" + n.ToString(CultureInfo.InvariantCulture);

    private static string Path(int n) => "/r/" + Id(n) + ".yaml";

    private static string Leaf(int n) =>
        $"schema: 1\nid: {Id(n)}\nversion: 1\ninitial: produce\nstates:\n"
        + "  produce: { kind: active, step: step.produce, timeout: { after: 4h, then: park }, next: \"Produce the Step output.\" }\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n  - { from: produce, to: done, on: step.completed }\n";

    private static string Parent(int n, string child) =>
        $"schema: 1\nid: {Id(n)}\nversion: 1\ninitial: running\nstates:\n"
        + $"  running: {{ kind: wait, waitFor: {{ child: {{ recipe: {child}, inputs: {{}}, onParked: propagate }} }}, timeout: {{ after: 14d, then: park }}, next: \"Waiting for {{child}}.\" }}\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n  - { from: running, to: done, on: wait.satisfied }\n";
}
