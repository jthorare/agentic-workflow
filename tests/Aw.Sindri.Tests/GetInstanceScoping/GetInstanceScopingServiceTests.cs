using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.GetInstanceScoping;

/// <summary>'GetInstanceScoping' (scoping spec API2): the stop view over an instance's facts, scoped like every instance read.</summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceScopingServiceTests
{
    private const string Item = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private sealed class Contents(IReadOnlyDictionary<string, string> bySha) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(bySha[fact.Sha256])));
    }

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("sk-live-123", "[redacted]", StringComparison.Ordinal);
    }

    private static (InstanceView View, Contents Contents) Instance(string recipe, ScopingFacts facts)
    {
        var view = StatusCases.View("desk-task");
        var shas = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var (key, body) in facts)
        {
            var sha = (++n).ToString("x64", System.Globalization.CultureInfo.InvariantCulture);
            shas["fact:" + key] = sha;
            text[sha] = body;
        }

        return (view with { ItemId = ItemKey.From(Item), Recipe = view.Recipe with { Id = RecipeId.From(recipe) }, FactShas = shas }, new Contents(text));
    }

    private static async Task<Result<ScopingView>> Read(string recipe, ScopingFacts facts, CallerContext? caller = null)
    {
        var (view, contents) = Instance(recipe, facts);
        var instances = new SomeInstances(view);
        var service = new GetInstanceScopingService(new ScopingInstances(instances, instances, new ListInstances.FixedCaller(caller ?? new CallerContextBuilder().Human().Build())), contents, new Redacting());
        return await service.Handle(new GetInstanceScopingRequest(view.Id), CancellationToken.None);
    }

    [Fact]
    public async Task A_scoping_instances_facts_become_the_nine_section_stop_view()
    {
        var result = await Read("workflow.scoping", ScopingFacts.Expiry());

        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.Value.Sections.Count);
    }

    [Fact]
    public async Task The_view_is_live_before_evidence_with_the_sections_that_need_missing_facts_marked_not_yet()
    {
        var result = await Read("workflow.scoping", new ScopingFacts { ["brief"] = ScopingFacts.Expiry()["brief"] });

        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.Value.Sections.Count);
        Assert.Contains(result.Value.Sections, s => s.State == ScopingSectionState.NotYet);
    }

    [Fact]
    public async Task An_instance_of_another_recipe_is_SND_SCOPING_001()
    {
        var result = await Read("workflow.project", ScopingFacts.Expiry());

        Assert.Equal(ScopingErrors.NotAScopingInstance.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_session_of_any_repo_reads_like_a_human()
    {
        var own = await Read("workflow.scoping", ScopingFacts.Expiry(), new CallerContextBuilder().Session().InRepo("alpha").Build());
        var other = await Read("workflow.scoping", ScopingFacts.Expiry(), new CallerContextBuilder().Session().InRepo("beta").Build());

        Assert.True(own.IsSuccess);
        Assert.True(other.IsSuccess);
    }

    [Fact]
    public async Task Secrets_in_a_fact_are_scrubbed_before_the_view_is_built()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"]![0]!["text"] = "WHEN a member shares THE system SHALL keep sk-live-123");

        var result = await Read("workflow.scoping", facts);

        Assert.DoesNotContain("sk-live-123", result.Value.Sections.SelectMany(static s => s.Rows).Select(static r => r.Text).Aggregate(string.Empty, static (a, b) => a + b), StringComparison.Ordinal);
    }
}
