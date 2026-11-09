using Aw.Context.Core.Models;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.Models;

/// <summary>A prompt block asks the routed model port under the 'workflowPrompt' route and gets the answer text back.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphModelPortAdapterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_prompt_is_asked_with_the_workflow_prompt_purpose_and_the_answer_text_comes_back()
    {
        var port = new FakeModelPort { Answer = "{\"drifted\":false}" };

        var answer = await new GraphModelPortAdapter(port).Ask("did the contract drift?", Token);

        Assert.Equal("{\"drifted\":false}", answer.Value);
        var request = Assert.Single(port.Requests);
        Assert.Equal(ModelPurpose.WorkflowPrompt, request.Purpose);
        Assert.Equal("did the contract drift?", request.Prompt);
    }

    [Fact]
    public async Task A_refused_ask_is_returned_as_its_error()
    {
        var port = new FakeModelPort { Fail = static _ => SindriErrors.NotRunning.With("no harness") };

        var answer = await new GraphModelPortAdapter(port).Ask("hello", Token);

        Assert.Equal(SindriErrors.NotRunning.Code, answer.Error.Code);
    }
}
