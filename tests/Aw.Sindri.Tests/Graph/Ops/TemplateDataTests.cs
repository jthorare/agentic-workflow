using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Tests.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Ops;

/// <summary>#3509: the Weekday check and Daily digest starters read real data (an agent step before the Prompt) instead of asking a model about nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class TemplateDataTests
{
    private const string Repo = "web";

    private static GraphValidator RealValidator() => GraphFixtures.Validator(
        blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: [new CreateItemBlock(new FlowFakes.RecordingItemPort()), new CodeBlock(runner: null!)]).Types,
        cron: (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
        condition: text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []));

    private static GraphSpec Built(string name) => GraphTemplates.Find(name, Repo)!.Spec;

    // the ids of every node with a path to 'node' (its upstream)
    private static HashSet<string> Upstream(GraphSpec spec, string node)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var todo = new Stack<string>([node]);
        while (todo.Count > 0)
        {
            var at = todo.Pop();
            foreach (var edge in spec.Edges.Where(e => string.Equals(e.To.Value, at, StringComparison.Ordinal)))
            {
                if (seen.Add(edge.From.Value))
                {
                    todo.Push(edge.From.Value);
                }
            }
        }

        return seen;
    }

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public void A_data_template_has_an_agent_step_upstream_of_its_prompt_in_the_first_repo(string name)
    {
        var spec = Built(name);

        var prompt = Assert.Single(spec.Nodes, static n => n.Type == "prompt");
        var agents = spec.Nodes.Where(n => n.Type == "run_agent" && Upstream(spec, prompt.Id.Value).Contains(n.Id.Value)).ToList();
        var agent = Assert.Single(agents);
        Assert.Equal(Repo, agent.Config["repo"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public void The_prompt_question_reads_the_agent_output_and_the_spec_validates(string name)
    {
        var spec = Built(name);

        var agent = Assert.Single(spec.Nodes, static n => n.Type == "run_agent");
        var question = Assert.Single(spec.Nodes, static n => n.Type == "prompt").Config["prompt"]!.GetValue<string>();
        Assert.Contains("{{" + agent.Id.Value + ".", question, StringComparison.Ordinal);
        var result = RealValidator().Validate(spec, GraphMode.Activate);
        Assert.True(result.Passed, string.Join("; ", result.Blocking));
    }

    [Fact]
    public void The_templates_that_read_no_data_take_no_repo()
    {
        // hello-sindri and manual-notify have no agent step, so a profile with no repo (a null first repo) still builds them (FirstRunSetup keeps calling Find(name) for hello-sindri and fills create_item's repo itself)
        Assert.NotNull(GraphTemplates.Find("manual-notify", firstRepo: null));
        var hello = GraphTemplates.Find("hello-sindri", firstRepo: null)!.Spec;
        Assert.DoesNotContain(hello.Nodes, static n => n.Type == "run_agent");
        Assert.Contains(hello.Nodes, static n => n.Type == "create_item");
    }
}
