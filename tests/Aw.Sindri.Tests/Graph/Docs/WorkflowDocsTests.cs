using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Docs;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Infrastructure.WorkflowDocs;
using Aw.Sindri.Tests.Graph.Blocks;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Graph.Docs;

[Lane(TestLane.Unit)]
public sealed class WorkflowDocsTests
{
    private static readonly string[] _concepts = ["spec", "references", "conditions", "concurrency", "runs"];

    private static GraphBlockRegistry Registry() => GraphBlockRegistry.Standard(
        new BlockFakes.RecordingNotifyPort(),
        new BlockFakes.ScriptedModelPort("{}"),
        more: [.. GraphBlockRegistry.FlowBlocks(new FlowFakes.MemorySeenStore(), new FlowFakes.RecordingItemPort(), new FlowFakes.ScriptedCallPort(), new FlowFakes.ScriptedHttpPort()),
            .. GraphBlockRegistry.ParkingBlocks(new BlockFakes.RecordingQuestionPort(), new FakeTimeProvider()),
            .. GraphBlockRegistry.ResultBlocks(new FlowFakes.ScriptedResultPort())]);

    private static GraphValidator Validator() => new(new GraphValidationContext(
        Registry().Types,
        new GraphFixtures.RepoWorld(GraphLookup.Known),
        (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
        text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, [])));

    private static string Top(string question) => WorkflowDocSearch.Search(EmbeddedWorkflowDocs.Load(), question)[0].Page.Slug;

    [Fact]
    public void Every_block_page_has_an_example_spec_that_validates()
    {
        var pages = EmbeddedWorkflowDocs.Load().Where(static p => p.BlockType is not null).ToList();
        Assert.NotEmpty(pages);
        foreach (var page in pages)
        {
            var examples = page.JsonExamples();
            Assert.True(examples.Count > 0, $"page '{page.Slug}' has no json example");
            foreach (var json in examples)
            {
                var spec = GraphJson.Parse(json);
                Assert.True(spec.IsSuccess, $"page '{page.Slug}': {(spec.IsError ? spec.Error.Message : string.Empty)}");
                Assert.Contains(spec.Value.Nodes, n => string.Equals(n.Type, page.BlockType, StringComparison.Ordinal));
                var result = Validator().Validate(spec.Value, GraphMode.Activate);
                Assert.True(result.Passed, $"page '{page.Slug}': {string.Join("; ", result.Findings)}");
            }
        }
    }

    [Fact]
    public void Every_json_example_on_a_concept_page_validates()
    {
        var pages = EmbeddedWorkflowDocs.Load().Where(static p => p.BlockType is null).ToList();
        Assert.Contains(pages, static p => p.JsonExamples().Count > 0);
        foreach (var json in pages.SelectMany(static p => p.JsonExamples()))
        {
            var spec = GraphJson.Parse(json);
            Assert.True(spec.IsSuccess, spec.IsError ? spec.Error.Message : string.Empty);
            var result = Validator().Validate(spec.Value, GraphMode.Activate);
            Assert.True(result.Passed, string.Join("; ", result.Findings));
        }
    }

    [Fact]
    public void Every_registered_block_has_a_page()
    {
        var pages = EmbeddedWorkflowDocs.Load();

        foreach (var type in Registry().Types.Keys)
        {
            Assert.Contains(pages, p => string.Equals(p.BlockType, type, StringComparison.Ordinal));
        }

        foreach (var block in pages.Select(static p => p.BlockType).OfType<string>())
        {
            Assert.True(Registry().Types.ContainsKey(block), $"page 'block-{block}' describes a block this build does not have");
        }
    }

    [Fact]
    public void The_concept_pages_are_there_and_each_has_a_title()
    {
        var pages = EmbeddedWorkflowDocs.Load();

        foreach (var id in _concepts)
        {
            var page = Assert.Single(pages, p => string.Equals(p.Slug, id, StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(page.Title));
        }
    }

    // why: a timer question must land on the schedule trigger, not the polling trigger ('connection_event') next to it
    [Fact]
    public void A_question_about_running_on_a_timer_finds_the_schedule_block_page()
        => Assert.Equal("block-schedule", Top("how do I run a workflow every weekday morning on a cron timer"));

    [Fact]
    public void A_question_about_telling_the_owner_finds_the_notify_block_page()
        => Assert.Equal("block-notify", Top("send the owner a notification in the Needs-you list"));

    [Fact]
    public void A_question_about_asking_the_model_finds_the_prompt_block_page()
        => Assert.Equal("block-prompt", Top("ask the model a question and get JSON back"));

    [Fact]
    public void A_question_about_overlapping_runs_finds_the_concurrency_page()
        => Assert.Equal("concurrency", Top("what happens when a second run starts while one is in flight, queue or unlimited"));

    [Fact]
    public void A_question_with_no_matching_word_finds_nothing_and_the_hit_count_is_capped()
    {
        var pages = EmbeddedWorkflowDocs.Load();

        Assert.Empty(WorkflowDocSearch.Search(pages, "zzzzqqqq xylophone"));
        Assert.Empty(WorkflowDocSearch.Search(pages, "   "));
        Assert.Equal(2, WorkflowDocSearch.Search(pages, "workflow node block", max: 2).Count);
    }

    [Fact]
    public void A_word_no_page_contains_does_not_hide_the_pages_the_other_words_find()
    {
        var pages = new[]
        {
            new WorkflowDocPage("block-notify", "Notify", "# Notify\n\nSends you a message."),
            new WorkflowDocPage("spec", "The spec", "# The spec\n\nNodes and edges."),
        };

        var hits = WorkflowDocSearch.Search(pages, "notify slack");

        Assert.Equal(["block-notify"], hits.Select(static h => h.Page.Slug), StringComparer.Ordinal);
        Assert.False(double.IsNaN(hits[0].Score));
    }

    [Fact]
    public void Search_orders_by_score_then_page_slug_and_a_heading_beats_a_passing_mention()
    {
        var pages = new[]
        {
            new WorkflowDocPage("b-page", "Other", "# Other\n\nA note that mentions retries once."),
            new WorkflowDocPage("a-page", "Retries", "# Retries\n\nHow retries work."),
            new WorkflowDocPage("c-page", "Third", "# Third\n\nNothing here."),
        };

        var hits = WorkflowDocSearch.Search(pages, "retries");

        Assert.Equal(["a-page", "b-page"], hits.Select(static h => h.Page.Slug), StringComparer.Ordinal);
        Assert.True(hits[0].Score > hits[1].Score);
    }

    [Fact]
    public void JsonExamples_returns_each_json_fence_and_ignores_other_fences()
    {
        var page = new WorkflowDocPage("block-x", "X", "# X\n\n```json\n{\"a\":1}\n```\n\n```text\nnot json\n```\n\n```json\n{\"b\":2}\n```\n");

        Assert.Equal(["{\"a\":1}", "{\"b\":2}"], page.JsonExamples());
        Assert.Equal("x", page.BlockType);
        Assert.Null(new WorkflowDocPage("spec", "Spec", "# Spec").BlockType);
    }
}
