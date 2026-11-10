using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Events;

/// <summary>The live event stream sends 'workflow.question' and 'workflow.answered' to a client with run, node and kind only (issue 2426): the Needs-you row updates live, and no question, choice or answer text reaches the frame.</summary>
public sealed class WorkflowQuestionStreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_stream_sends_the_question_without_its_text()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, Token);
        await ViewScenarios.Append(host, EventTypes.WorkflowQuestion, new WorkflowQuestionPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("ask"), "PRIVATE-QUESTION?", ["PRIVATE-CHOICE"], "PRIVATE-DEFAULT", TimeoutAt: null, "service:workflow"), "workflow.question:nightly@3:12:ask", Token);

        var frames = await StreamScenarios.Frames(host, "/sindri/events?types=workflow.question", ItemScenarios.Human(host), Token);

        Assert.Contains("\"type\": \"workflow.question\"", frames, StringComparison.Ordinal);
        Assert.Contains("\"run\": \"nightly@3:12\"", frames, StringComparison.Ordinal);
        Assert.Contains("\"node\": \"ask\"", frames, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"question\"", frames, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE", frames, StringComparison.Ordinal);
        Assert.DoesNotContain("\"question\":", frames, StringComparison.Ordinal);
        Assert.DoesNotContain("\"choices\"", frames, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_stream_sends_the_answer_without_its_text()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, Token);
        await ViewScenarios.Append(host, EventTypes.WorkflowAnswered, new WorkflowAnsweredPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("ask"), "PRIVATE-ANSWER", "human:PRIVATE-CLIENT"), "workflow.answered:nightly@3:12:ask", Token);

        var frames = await StreamScenarios.Frames(host, "/sindri/events?types=workflow.answered", ItemScenarios.Human(host), Token);

        Assert.Contains("\"type\": \"workflow.answered\"", frames, StringComparison.Ordinal);
        Assert.Contains("\"run\": \"nightly@3:12\"", frames, StringComparison.Ordinal);
        Assert.Contains("\"node\": \"ask\"", frames, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"question\"", frames, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE", frames, StringComparison.Ordinal);
        Assert.DoesNotContain("\"answer\"", frames, StringComparison.Ordinal);
    }
}
