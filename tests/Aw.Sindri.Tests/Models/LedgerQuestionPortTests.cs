using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Infrastructure.Text;

namespace Aw.Sindri.Tests.Models;

/// <summary>An ask_owner node's question becomes one 'workflow.question' event; asking again adds nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class LedgerQuestionPortTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@3:12");
    private static readonly NodeId _node = NodeId.From("ask");

    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LedgerQuestionPort Port() => new(_h.Store, _h.Held, _h.Events, _h.Callers, new RedactingScrubber(Redactor.Default));

    [Fact]
    public async Task A_post_appends_one_event_with_the_question_choices_default_and_deadline()
    {
        _ = await _h.HoldLease();
        var deadline = new DateTimeOffset(2026, 3, 7, 14, 0, 0, TimeSpan.Zero);

        var posted = await Port().Post(new QuestionAsk(_run, _node, "Ship the release?", ["yes", "no"], "no", deadline), Token);

        Assert.True(posted.IsSuccess);
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal(EventTypes.WorkflowQuestion, recorded.Envelope.Type);
        Assert.Equal("workflow.question:nightly@3:12:ask", recorded.IdempotencyKey);
        var payload = JsonNode.Parse(recorded.Envelope.Payload)!;
        Assert.Equal(("nightly@3:12", "ask", "Ship the release?", "no"), (payload["run"]!.GetValue<string>(), payload["node"]!.GetValue<string>(), payload["question"]!.GetValue<string>(), payload["default"]!.GetValue<string>()));
        Assert.Equal(["yes", "no"], payload["choices"]!.AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_secret_in_the_question_choices_or_default_is_scrubbed_in_the_ledger_row()
    {
        _ = await _h.HoldLease();
        var secret = "ghp_" + new string('a', 36);

        _ = await Port().Post(new QuestionAsk(_run, _node, $"Use {secret}?", [$"yes {secret}", "no"], $"no {secret}", TimeoutAt: null), Token);

        var stored = Assert.Single(_h.Store.Events).Envelope.Payload;
        Assert.DoesNotContain(secret, stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_identical_post_adds_nothing()
    {
        _ = await _h.HoldLease();
        var port = Port();
        _ = await port.Post(new QuestionAsk(_run, _node, "Ship?", [], Default: null, TimeoutAt: null), Token);

        var again = await port.Post(new QuestionAsk(_run, _node, "Ship?", [], Default: null, TimeoutAt: null), Token);

        Assert.True(again.IsSuccess);
        _ = Assert.Single(_h.Store.Events);
    }

    [Fact]
    public async Task A_post_without_the_lease_is_refused_and_appends_nothing()
    {
        var posted = await Port().Post(new QuestionAsk(_run, _node, "Ship?", [], Default: null, TimeoutAt: null), Token);

        Assert.True(posted.IsError);
        Assert.Empty(_h.Store.Events);
    }
}
