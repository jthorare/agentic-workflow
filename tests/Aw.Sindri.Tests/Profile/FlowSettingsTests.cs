using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'workflow.http.hosts' and 'workflow.callDepth' keys (agent-first PR 9): what a workflow 'http' node may reach and how deep one workflow may call another.</summary>
public sealed class FlowSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static SindriProfile With(IReadOnlyList<string>? hosts = null, int? depth = null) =>
        _valid with { Workflow = new WorkflowSettings { Http = hosts is null ? null : new HttpSettings(hosts), CallDepth = depth } };

    [Theory, Trait("Lane", "unit")]
    [InlineData("api.example.com")]
    [InlineData("a.b.c.example.org")]
    [InlineData("api-1.example.com")]
    public void A_plain_public_host_name_is_accepted(string host) => Assert.True(ProfileRules.Check(With([host])).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("app.localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("::1")]
    [InlineData("https://api.example.com")]
    [InlineData("api.example.com:8443")]
    [InlineData("api.example.com/path")]
    [InlineData("API.example.com")]
    [InlineData("-bad.example.com")]
    [InlineData("under_score.example.com")]
    public void A_host_that_is_not_a_plain_public_name_is_refused_naming_the_key(string host)
    {
        var error = ProfileRules.Check(With([host])).Error;

        Assert.Contains("workflow.http.hosts[0]", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_host_listed_twice_and_too_many_hosts_are_refused()
    {
        var twice = ProfileRules.Check(With(["a.example.com", "b.example.com", "a.example.com"])).Error;
        var tooMany = ProfileRules.Check(With([.. Enumerable.Range(0, 51).Select(static i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"h{i}.example.com"))])).Error;

        Assert.Contains("workflow.http.hosts[2]", twice.Message, StringComparison.Ordinal);
        Assert.Contains("at most 50", tooMany.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void A_call_depth_from_one_to_ten_is_accepted(int depth) => Assert.True(ProfileRules.Check(With(depth: depth)).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void A_call_depth_outside_one_to_ten_is_refused_naming_the_key(int depth) =>
        Assert.Contains("workflow.callDepth", ProfileRules.Check(With(depth: depth)).Error.Message, StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_each_flow_key_that_changes()
    {
        var live = ProfileRules.Check(_valid).Value;

        Assert.Equal(["workflow.callDepth", "workflow.http.hosts"], ProfileDiff.Between(live, With(["api.example.com"], depth: 2)));
        Assert.Equal(["workflow.http.hosts"], ProfileDiff.Between(With(["a.example.com"]), With(["a.example.com", "b.example.com"])));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_hash_changes_with_either_key()
    {
        var none = ProfileHashing.Of(Kernel.Json.AwJson.Serialize(ProfileRules.Check(_valid).Value, SliceHarness.Json));
        var hosts = ProfileHashing.Of(Kernel.Json.AwJson.Serialize(ProfileRules.Check(With(["a.example.com"])).Value, SliceHarness.Json));
        var depth = ProfileHashing.Of(Kernel.Json.AwJson.Serialize(ProfileRules.Check(With(depth: 2)).Value, SliceHarness.Json));

        Assert.NotEqual(none, hosts);
        Assert.NotEqual(none, depth);
        Assert.NotEqual(hosts, depth);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("sheet")]
    [InlineData("steps")]
    public void The_workflow_editor_takes_sheet_steps_or_nothing(string? editor) =>
        Assert.True(ProfileRules.Check(_valid with { Workflow = new WorkflowSettings { Editor = editor } }).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("grid")]
    [InlineData("Sheet")]
    [InlineData("")]
    public void Another_workflow_editor_is_refused_naming_the_key_and_both_values(string editor)
    {
        var error = ProfileRules.Check(_valid with { Workflow = new WorkflowSettings { Editor = editor } }).Error;

        Assert.Contains("workflow.editor", error.Message, StringComparison.Ordinal);
        Assert.Contains("'sheet'", error.Message, StringComparison.Ordinal);
        Assert.Contains("'steps'", error.Message, StringComparison.Ordinal);
    }
}
