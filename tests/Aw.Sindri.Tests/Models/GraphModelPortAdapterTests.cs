using Aw.Context.Core.Models;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
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

    [Fact]
    public async Task An_answer_reports_the_input_and_output_tokens_together()
    {
        const string Prompt = "did the contract drift?";
        const string Reply = "{\"drifted\":false}";
        var port = new FakeModelPort { Answer = Reply };
        var expected = ((Prompt.Length + 3L) / 4) + ((Reply.Length + 3L) / 4);   // why: the fake counts a token per four characters of the prompt and of the reply

        var answer = await ((IGraphModelPort)new GraphModelPortAdapter(port)).Answer(Prompt, Token);

        Assert.Equal(Reply, answer.Value.Text);
        Assert.Equal(expected, answer.Value.Tokens);
    }
}
