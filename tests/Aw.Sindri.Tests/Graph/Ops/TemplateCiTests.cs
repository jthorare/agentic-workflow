using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Tests.Graph.Blocks;
using Aw.Sindri.Tests.Toolkits;

namespace Aw.Sindri.Tests.Graph.Ops;

/// <summary>#3534: with a GitHub CI source, the Weekday check and Daily digest starters read the branch's latest runs through a 'ci' call_tool step; without one they stay as they were.</summary>
[Lane(TestLane.Unit)]
public sealed class TemplateCiTests
{
    private static readonly GitHubCiSource _source = new("github:work", "acme", "app", "main");

    private static GraphValidator RealValidator() => GraphFixtures.Validator(
        blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: [new CallToolBlock(new FakeRunningToolkits([]), new NoConnections())]).Types,
        cron: (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
        condition: text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []));

    private static GraphSpec WithCi(string name) => GraphTemplates.Find(name, _source)!.Spec;

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public void The_ci_step_calls_list_runs_on_the_source_branch_and_sits_between_the_schedule_and_the_prompt(string name)
    {
        var spec = WithCi(name);

        var ci = Assert.Single(spec.Nodes, static n => n.Type == "call_tool");
        Assert.Equal("ci", ci.Id.Value);
        Assert.Equal("github:work", ci.Config["connection"]!.GetValue<string>());
        Assert.Equal("list_runs", ci.Config["tool"]!.GetValue<string>());
        var arguments = ci.Config["arguments"]!.AsObject();
        Assert.Equal("acme", arguments["owner"]!.GetValue<string>());
        Assert.Equal("app", arguments["repo"]!.GetValue<string>());
        Assert.Equal("main", arguments["branch"]!.GetValue<string>());
        Assert.Equal("5", arguments["per_page"]!.GetValue<string>());
        var schedule = Assert.Single(spec.Nodes, static n => n.Type == "schedule");
        var prompt = Assert.Single(spec.Nodes, static n => n.Type == "prompt");
        Assert.Contains(spec.Edges, e => e.From == schedule.Id && e.To == ci.Id);
        Assert.Contains(spec.Edges, e => e.From == ci.Id && e.To == prompt.Id);
        Assert.DoesNotContain(spec.Edges, e => e.From == schedule.Id && e.To == prompt.Id);
    }

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public void The_prompt_reads_the_ci_result_and_keeps_the_schedule_date(string name)
    {
        var spec = WithCi(name);

        var schedule = Assert.Single(spec.Nodes, static n => n.Type == "schedule");
        var question = Assert.Single(spec.Nodes, static n => n.Type == "prompt").Config["prompt"]!.GetValue<string>();
        Assert.Contains("{{ci.result}}", question, StringComparison.Ordinal);
        Assert.Contains("{{" + schedule.Id.Value + ".scheduledFor}}", question, StringComparison.Ordinal);
    }

    [Fact]
    public void The_weekday_check_flags_a_branch_whose_newest_run_did_not_succeed()
    {
        var question = Assert.Single(WithCi("weekday-check").Nodes, static n => n.Type == "prompt").Config["prompt"]!.GetValue<string>();

        Assert.Contains("flagged", question, StringComparison.Ordinal);
        Assert.Contains("workflow", question, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public void An_empty_run_list_is_counted_as_passed_and_named_with_the_branch(string name)
    {
        var question = Assert.Single(WithCi(name).Nodes, static n => n.Type == "prompt").Config["prompt"]!.GetValue<string>();

        Assert.Contains("When there are no runs", question, StringComparison.Ordinal);
        Assert.Contains("no CI runs on main", question, StringComparison.Ordinal);
    }

    [Fact]
    public void A_branch_with_braces_gets_the_plain_template_so_the_create_still_validates()
    {
        var odd = new GitHubCiSource("github:work", "acme", "app", "x{{y}}");

        var spec = GraphTemplates.Find("weekday-check", odd)!.Spec;

        Assert.DoesNotContain(spec.Nodes, static n => n.Type == "call_tool");
        Assert.Equal(GraphTemplates.Find("weekday-check")!.Spec.Nodes.Select(static n => n.Id), spec.Nodes.Select(static n => n.Id));
        Assert.True(RealValidator().Validate(spec, GraphMode.Activate).Passed);
    }

    [Fact]
    public void The_weekday_check_still_flags_a_friday_with_the_plain_templates_reminder()
    {
        var question = Assert.Single(WithCi("weekday-check").Nodes, static n => n.Type == "prompt").Config["prompt"]!.GetValue<string>();

        Assert.Contains("when today is a Friday", question, StringComparison.Ordinal);
        Assert.Contains("Friday: review your open pull requests before the weekend.", question, StringComparison.Ordinal);
    }

    [Fact]
    public void The_daily_digest_says_whether_the_newest_run_passed()
    {
        var question = Assert.Single(WithCi("daily-digest").Nodes, static n => n.Type == "prompt").Config["prompt"]!.GetValue<string>();

        Assert.Contains("passed", question, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public void The_ci_variant_validates(string name)
    {
        var result = RealValidator().Validate(WithCi(name), GraphMode.Activate);

        Assert.True(result.Passed, string.Join("; ", result.Blocking));
    }

    [Fact]
    public void A_name_with_a_quote_in_the_source_stays_a_value_not_json_text()
    {
        var odd = new GitHubCiSource("github:work", "a\"b", "app\\x", "feat/\"q\"");

        var arguments = Assert.Single(GraphTemplates.Find("weekday-check", odd)!.Spec.Nodes, static n => n.Type == "call_tool").Config["arguments"]!.AsObject();

        Assert.Equal("a\"b", arguments["owner"]!.GetValue<string>());
        Assert.Equal("app\\x", arguments["repo"]!.GetValue<string>());
        Assert.Equal("feat/\"q\"", arguments["branch"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    [InlineData("manual-notify")]
    [InlineData("hello-sindri")]
    public void Without_a_source_every_template_is_what_it_is_today(string name)
    {
        Assert.Equal(GraphJson.Write(GraphTemplates.Find(name)!.Spec), GraphJson.Write(GraphTemplates.Find(name, ci: null)!.Spec));
        Assert.DoesNotContain(GraphTemplates.Find(name)!.Spec.Nodes, static n => n.Type == "call_tool");
    }

    [Theory]
    [InlineData("manual-notify")]
    [InlineData("hello-sindri")]
    public void The_other_templates_ignore_a_source(string name)
        => Assert.Equal(GraphJson.Write(GraphTemplates.Find(name)!.Spec), GraphJson.Write(GraphTemplates.Find(name, _source)!.Spec));

    // why: validation never calls a connection; every call is refused
    private sealed class NoConnections : IConnectionToolPort
    {
        public ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<IReadOnlyList<ConnectionTool>>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")));

        public ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<string>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")));
    }
}
