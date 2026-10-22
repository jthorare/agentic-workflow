using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>'effect.line.start-phase' (line spec LN-WF1): starts one phase as a linked instance, once per (line, phase, attempt), with its packet and base branch.</summary>
public sealed class StartPhaseEffectTests
{
    private const string Line = "T-3/line/1";
    private const string Next = """{"phase":"p2","attempt":1,"packet":{"phase":"p2","id":"p2","hash":"sha256:abc","dir":"phase-packets/p2"},"base":{"rule":"dependency-head","ref":"sindri/p1"},"stackParent":"p1"}""";

    private readonly InMemoryFactStore _facts = new();
    private readonly FakePhaseStarts _starts = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private StartPhaseEffect Effect() => new(_starts, _facts, _facts, new FakeTimeProvider(DateTimeOffset.Parse("2026-10-22T10:00:00Z", CultureInfo.InvariantCulture)));

    private async Task<JsonElement> Stored(string json)
    {
        var put = await _facts.Put(Encoding.UTF8.GetBytes(json), Token);
        return JsonSerializer.SerializeToElement(new { @ref = put.Value.Ref, sha256 = put.Value.Sha256 });
    }

    private async Task<EffectContext> Context(string next = Next, string recipe = "workflow.phase", bool lineContext = true)
    {
        var facts = new Dictionary<FactKey, JsonElement>();
        if (lineContext)
        {
            facts[FactKey.From("fact:lineContext")] = await Stored("""{"base":"main"}""");
        }

        return new EffectContext(
            BlockId.From("effect.line.start-phase"), "WFI-a:3:0", "c-1", FromEngine: false, Line, "T-3", Attempt: 1,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["phase"] = await Stored(next), ["recipe"] = JsonSerializer.SerializeToElement(recipe) },
            facts, Text: null);
    }

    private async Task<JsonNode?> Content(FactValue fact) =>
        JsonNode.Parse((await _facts.Read(fact, Token)).Value.Span);

    [Fact, Trait("Lane", "unit")]
    public async Task It_starts_the_phase_as_a_linked_instance_with_its_packet_and_base_branch()
    {
        var done = await Effect().Dispatch(await Context(), Token);

        var start = Assert.Single(_starts.Started);
        Assert.Equal(("p2", 1, "workflow.phase", Line), (start.Phase, start.Attempt, start.Recipe.Value, start.Line.Value));
        var packet = await Content(start.Inputs[FactKey.From("fact:phasePacket")]);
        Assert.Equal(("p2", "sha256:abc", "dependency-head", "sindri/p1", "p1"),
            (packet!["phase"]!.GetValue<string>(), packet["hash"]!.GetValue<string>(), packet["base"]!["rule"]!.GetValue<string>(), packet["base"]!["ref"]!.GetValue<string>(), packet["stackParent"]!.GetValue<string>()));
        var context = await _facts.Read(start.Inputs[FactKey.From("fact:lineContext")], Token);
        Assert.Equal("""{"base":"main"}""", Encoding.UTF8.GetString(context.Value.Span));
        Assert.Equal(("started", false), (done.Value.Reply?.Value, done.Value.Existed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task It_replies_started_with_the_phase_event_fact()
    {
        var done = await Effect().Dispatch(await Context(), Token);

        var reply = done.Value.Facts![FactKey.From("fact:phaseEvent")];
        var content = JsonNode.Parse((await _facts.Read(new FactValue(reply.Ref, reply.Sha256, HeadSha: null), Token)).Value.Span);
        Assert.Equal((Line, "p2", 1, "started", "T-3/line/1/phase.p2.1"), (content!["line"]!.GetValue<string>(), content["phase"]!.GetValue<string>(), content["attempt"]!.GetValue<int>(), content["status"]!.GetValue<string>(), content["instance"]!.GetValue<string>()));
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T10:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse(content["at"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal("line.phaseChanged", done.Value.Event?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dispatching_the_same_phase_and_attempt_again_starts_nothing_twice()
    {
        _ = await Effect().Dispatch(await Context(), Token);

        var again = await Effect().Dispatch(await Context(), Token);

        _ = Assert.Single(_starts.Started);
        Assert.Equal(("started", true), (again.Value.Reply?.Value, again.Value.Existed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_retry_is_the_next_attempt_and_starts_again()
    {
        _ = await Effect().Dispatch(await Context(), Token);

        _ = await Effect().Dispatch(await Context(Next.Replace("\"attempt\":1", "\"attempt\":2", StringComparison.Ordinal)), Token);

        Assert.Equal([1, 2], _starts.Started.Select(static s => s.Attempt));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_line_without_a_context_starts_the_phase_without_one()
    {
        _ = await Effect().Dispatch(await Context(lineContext: false), Token);

        var start = Assert.Single(_starts.Started);
        Assert.False(start.Inputs.ContainsKey(FactKey.From("fact:lineContext")));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"phase":"P2","attempt":1,"packet":{}}""")]
    [InlineData("""{"phase":"p2","attempt":0,"packet":{}}""")]
    [InlineData("""{"phase":"p2","attempt":1}""")]
    [InlineData("not json")]
    public async Task A_next_phase_that_is_not_one_is_refused_without_starting(string next)
    {
        var done = await Effect().Dispatch(await Context(next), Token);

        Assert.Equal("SND-WF-010", done.Error.Code);
        Assert.Empty(_starts.Started);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("Not A Recipe")]
    public async Task A_missing_or_bad_recipe_is_refused_without_starting(string recipe)
    {
        var done = await Effect().Dispatch(await Context(recipe: recipe), Token);

        Assert.Equal("SND-WF-010", done.Error.Code);
        Assert.Empty(_starts.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_start_that_fails_is_the_effects_error_so_the_activity_retries()
    {
        _starts.Failure = SindriErrors.ApiInternal.With("temporal is down");

        var done = await Effect().Dispatch(await Context(), Token);

        Assert.True(done.IsError);
        Assert.Contains("temporal is down", done.Error.Message, StringComparison.Ordinal);
    }
}
