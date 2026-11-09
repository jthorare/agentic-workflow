using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Tests.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Ops;

[Lane(TestLane.Unit)]
public sealed class TemplatesTests
{
    private static GraphValidator RealValidator() => GraphFixtures.Validator(
        blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}")).Types,
        cron: (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
        condition: text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []));

    [Fact]
    public void Every_template_validates()
    {
        Assert.NotEmpty(GraphTemplates.All);
        foreach (var template in GraphTemplates.All)
        {
            var result = RealValidator().Validate(template.Spec, GraphMode.Activate);

            Assert.True(result.Passed, $"{template.Name}: {string.Join("; ", result.Blocking)}");
            Assert.False(string.IsNullOrWhiteSpace(template.Description));
        }
    }

    [Fact]
    public void Template_names_are_unique_and_Find_returns_each_by_name()
    {
        var names = GraphTemplates.All.Select(t => t.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.Equal(name, GraphTemplates.Find(name)!.Name));
        Assert.Null(GraphTemplates.Find("no-such-template"));
    }

    [Fact]
    public void The_weekday_check_template_runs_on_weekdays_and_notifies_only_when_flagged()
    {
        var spec = GraphTemplates.Find("weekday-check")!.Spec;

        var trigger = Assert.Single(spec.Nodes, n => n.Type == "schedule");
        Assert.Contains("1-5", trigger.Config["cron"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains(spec.Edges, e => e.Output == "true" && spec.Nodes.Single(n => n.Id == e.To).Type == "notify");
    }

    [Fact]
    public void A_template_is_a_copy_so_a_caller_changing_it_does_not_change_the_next_caller_s()
    {
        var first = GraphTemplates.Find("weekday-check")!.Spec;
        first.Nodes[0].Config["cron"] = "* * * * *";

        var second = GraphTemplates.Find("weekday-check")!.Spec;

        Assert.NotEqual("* * * * *", second.Nodes[0].Config["cron"]!.GetValue<string>(), StringComparer.Ordinal);
    }
}
