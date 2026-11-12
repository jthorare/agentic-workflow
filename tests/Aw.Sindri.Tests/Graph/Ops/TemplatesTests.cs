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
        blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: [new CreateItemBlock(new FlowFakes.RecordingItemPort()), new CodeBlock(runner: null!)]).Types,
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
    public void A_schedule_template_uses_this_machines_timezone_not_a_fixed_one()
    {
        var expected = TimeZoneInfo.Local.HasIanaId ? TimeZoneInfo.Local.Id : "UTC";

        foreach (var trigger in GraphTemplates.All.SelectMany(static t => t.Spec.Nodes).Where(static n => n.Type == "schedule"))
        {
            Assert.Equal(expected, trigger.Config["timezone"]!.GetValue<string>());
        }
    }

    [Fact]
    public void A_template_is_a_copy_so_a_caller_changing_it_does_not_change_the_next_caller_s()
    {
        var first = GraphTemplates.Find("weekday-check")!.Spec;
        first.Nodes[0].Config["cron"] = "* * * * *";

        var second = GraphTemplates.Find("weekday-check")!.Spec;

        Assert.NotEqual("* * * * *", second.Nodes[0].Config["cron"]!.GetValue<string>(), StringComparer.Ordinal);
    }

    [Fact]
    public void The_hello_sindri_template_needs_no_account_model_or_network_and_makes_one_item()
    {
        var spec = GraphTemplates.Find("hello-sindri")!.Spec;

        Assert.Equal(["manual", "code", "notify", "create_item"], spec.Nodes.Select(static n => n.Type), StringComparer.Ordinal);
        Assert.Equal("60s", spec.Nodes.Single(static n => n.Type == "code").Config["timeout"]!.GetValue<string>());
        string[] needsSomethingOutside = ["prompt", "run_agent", "http", "schedule", "connection_event"];
        Assert.DoesNotContain(spec.Nodes, n => needsSomethingOutside.Contains(n.Type, StringComparer.Ordinal));
    }
}
