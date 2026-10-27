using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.GetInstanceFact;

/// <summary>'GetInstanceFact' (API spec §3.8): the scrubbed body behind one fact row, capped at 256 KiB, an object stays an object when cut.</summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceFactServiceTests
{
    private const string Sha = "0000000000000000000000000000000000000000000000000000000000000001";

    private sealed class Contents(string text) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(text)));
    }

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("sk-live-123", "[redacted]", StringComparison.Ordinal);
    }

    private static InstanceView With(string key, FactType type, bool produced = true)
    {
        var view = StatusCases.View("desk-task");
        var row = view.Facts[0] with { Key = key, Type = type };
        return view with { Facts = [row], FactShas = produced ? new Dictionary<string, string>(StringComparer.Ordinal) { [key] = Sha } : null };
    }

    private static async Task<Result<FactView>> Read(InstanceView view, string key, string content, CallerContext? caller = null, WorkflowInstanceId? asking = null)
    {
        var instances = new SomeInstances(view);
        var service = new GetInstanceFactService(instances, instances, new Contents(content), new Redacting(), new ListInstances.FixedCaller(caller ?? new CallerContextBuilder().Human().Build()));
        return await service.Handle(new GetInstanceFactRequest(asking ?? view.Id, key), CancellationToken.None);
    }

    [Fact]
    public async Task Text_fact_serves_a_string_body()
    {
        var result = await Read(With("fact:spec", FactType.Text), "fact:spec", "the spec");

        Assert.Equal(("fact:spec", FactType.Text, "the spec", false), (result.Value.Key, result.Value.Type, result.Value.Body.GetString(), result.Value.Truncated));
    }

    [Fact]
    public async Task Packet_fact_serves_an_object_body()
    {
        var result = await Read(With("fact:plan", FactType.Packet), "fact:plan", """{"tasks":[{"id":"t1"}],"checks":{"PKT-1":"pass"}}""");

        Assert.Equal(JsonValueKind.Object, result.Value.Body.ValueKind);
        Assert.Equal("t1", result.Value.Body.GetProperty("tasks")[0].GetProperty("id").GetString());
        Assert.False(result.Value.Truncated);
    }

    [Fact]
    public async Task Body_over_256KiB_is_cut_and_stays_an_object()
    {
        var entries = string.Join(',', Enumerable.Range(0, 200).Select(static i => string.Create(CultureInfo.InvariantCulture, $"\"k{i:D3}\":\"{new string('x', 4000)}\"")));

        var result = await Read(With("fact:plan", FactType.Packet), "fact:plan", "{" + entries + "}");

        Assert.True(result.Value.Truncated);
        Assert.Equal(JsonValueKind.Object, result.Value.Body.ValueKind);
        var kept = result.Value.Body.EnumerateObject().Select(static p => p.Name).ToList();
        Assert.InRange(kept.Count, 1, 199);
        Assert.Equal([.. Enumerable.Range(0, kept.Count).Select(static i => string.Create(CultureInfo.InvariantCulture, $"k{i:D3}"))], kept, StringComparer.Ordinal);   // the tail is what is dropped
        Assert.True(Encoding.UTF8.GetByteCount(result.Value.Body.GetRawText()) <= 256 * 1024);
    }

    [Fact]
    public async Task A_text_body_over_the_cap_is_cut_on_a_character_boundary()
    {
        var result = await Read(With("fact:spec", FactType.Text), "fact:spec", new string('é', 200_000));

        Assert.True(result.Value.Truncated);
        Assert.True(Encoding.UTF8.GetByteCount(result.Value.Body.GetString()!) <= 256 * 1024);
    }

    [Theory]
    [InlineData("fact:unknown", true)]
    [InlineData("fact:spec", false)]
    public async Task Undeclared_key_or_one_not_produced_yet_is_SND_ITEM_404(string key, bool produced)
    {
        var result = await Read(With("fact:spec", FactType.Text, produced), key, "x");

        Assert.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    private static InstanceView Produced(string key, params string[] declared)
    {
        var view = StatusCases.View("desk-task");
        return view with { Facts = [.. declared.Select(k => view.Facts[0] with { Key = k })], FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { [key] = Sha } };
    }

    [Fact]
    public async Task A_produced_fact_the_recipe_does_not_declare_is_served()
    {
        var result = await Read(Produced("fact:questions", "fact:spec"), "fact:questions", """[{"id":"Q1","question":"Which?","options":[{"id":"Q1.a","answer":"A"}]}]""");

        Assert.Equal(("fact:questions", FactType.Payload, JsonValueKind.Array, "Q1.a", false),
            (result.Value.Key, result.Value.Type, result.Value.Body.ValueKind, result.Value.Body[0].GetProperty("options")[0].GetProperty("id").GetString(), result.Value.Truncated));
    }

    [Fact]
    public async Task An_undeclared_produced_fact_that_is_not_json_is_served_as_text()
    {
        var result = await Read(Produced("fact:note", "fact:spec"), "fact:note", "plain words");

        Assert.Equal((FactType.Text, "plain words"), (result.Value.Type, result.Value.Body.GetString()));
    }

    [Fact]
    public async Task A_key_nothing_produced_is_still_SND_ITEM_404_when_other_undeclared_facts_are()
    {
        var result = await Read(Produced("fact:questions", "fact:spec"), "fact:answers", "x");

        Assert.Equal((SindriErrors.ItemNotFound.Code, "no fact 'fact:answers' on the instance"), (result.Error.Code, result.Error.Message));
    }

    [Fact]
    public async Task Secrets_in_a_fact_are_scrubbed()
    {
        var text = await Read(With("fact:spec", FactType.Text), "fact:spec", "token sk-live-123 here");
        var packet = await Read(With("fact:plan", FactType.Packet), "fact:plan", """{"env":{"KEY":"sk-live-123"}}""");

        Assert.Equal("token [redacted] here", text.Value.Body.GetString());
        Assert.Equal("[redacted]", packet.Value.Body.GetProperty("env").GetProperty("KEY").GetString());
    }

    private const string Item = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task A_session_reads_a_fact_of_any_repo()
    {
        var view = With("fact:spec", FactType.Text) with { ItemId = ItemKey.From(Item) };

        var own = await Read(view, "fact:spec", "the spec", new CallerContextBuilder().Session().InRepo("alpha").Build());
        var other = await Read(view, "fact:spec", "the spec", new CallerContextBuilder().Session().InRepo("beta").Build());

        Assert.Equal("the spec", own.Value.Body.GetString());
        Assert.Equal("the spec", other.Value.Body.GetString());
    }

    [Fact]
    public async Task An_unknown_instance_answers_not_found_with_the_id_asked_for()
    {
        var view = With("fact:spec", FactType.Text) with { ItemId = ItemKey.From(Item) };
        var unknown = WorkflowInstanceId.From("WFI-" + new string('9', 32));

        var missing = await Read(view, "fact:spec", "x", asking: unknown);

        Assert.Equal((SindriErrors.ItemNotFound.Code, $"No workflow instance {unknown.Value} in this repo."), (missing.Error.Code, missing.Error.Message));
    }

    [Fact]
    public async Task A_packet_whose_stored_content_is_not_json_is_served_as_text()
    {
        var result = await Read(With("fact:plan", FactType.Packet), "fact:plan", "not { json");

        Assert.Equal(("not { json", JsonValueKind.String, false), (result.Value.Body.GetString(), result.Value.Body.ValueKind, result.Value.Truncated));
    }

    [Fact]
    public async Task A_very_large_text_is_cut_in_one_pass()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var result = await Read(With("fact:spec", FactType.Text), "fact:spec", new string('é', 4_000_000));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"cutting took {clock.Elapsed}");   // a quadratic cut takes minutes here; 10 s leaves room for a loaded CI runner
        Assert.Equal((true, 256 * 1024 / 2), (result.Value.Truncated, result.Value.Body.GetString()!.Length));   // 2 bytes a character, cut exactly at the budget
    }

    [Fact]
    public async Task An_object_of_very_many_small_entries_is_cut_in_one_pass_to_the_exact_budget()
    {
        var entries = string.Join(',', Enumerable.Range(0, 120_000).Select(static i => string.Create(CultureInfo.InvariantCulture, $"\"k{i}\":1")));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var result = await Read(With("fact:plan", FactType.Packet), "fact:plan", "{" + entries + "}");

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"cutting took {clock.Elapsed}");   // a quadratic cut takes minutes here; 10 s leaves room for a loaded CI runner
        Assert.True(result.Value.Truncated);
        Assert.InRange(Encoding.UTF8.GetByteCount(result.Value.Body.GetRawText()), (256 * 1024) - 16, 256 * 1024);
    }
}
