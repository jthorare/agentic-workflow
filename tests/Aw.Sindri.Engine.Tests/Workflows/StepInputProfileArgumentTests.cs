using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A Step input's '{ profile: key }' argument reaches the input (#399): the 'FillInput' activity hands it on as a reference to resolve against the approved profile, instead of dropping it.</summary>
[Lane(TestLane.Unit)]
public sealed class StepInputProfileArgumentTests
{
    private const string Key = "workflow.scoping.historyWindow";
    private static readonly BlockId _input = BlockId.From("input.context.history");
    private static readonly BlockId _step = BlockId.From("step.scope.gather");

    private sealed class CapturingRunner : IStepInputRunner
    {
        public StepInputContext? Seen { get; private set; }

        public ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken)
        {
            Seen = context;
            return ValueTask.FromResult<Result<FilledOutput>>(new FilledOutput(new FilledInput(_input, index, []), new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal))));
        }
    }

    [Fact]
    public async Task A_profile_argument_reaches_the_input_naming_its_key()
    {
        var runner = new CapturingRunner();
        var texts = new MemoryInjectionTexts();
        var activities = new StepInputActivities(runner, texts, new StepContexts(texts, runner, new ItemReposStub()));
        var with = new Dictionary<string, WithValue>(StringComparer.Ordinal) { ["window"] = new WithValue(Literal: null, Fact: null, Profile: Key) };
        var request = new FillInputRequest("T-1/scoping/1", "gather", "1.1", 1, _input, _step, with, new Dictionary<FactKey, FactValue>(), "web");

        _ = await activities.Fill(request, TestContext.Current.CancellationToken);

        Assert.True(runner.Seen!.Arguments.TryGetValue("window", out var window), "the window argument was dropped before the input");
        Assert.Contains(Key, window.GetRawText(), StringComparison.Ordinal);
    }
}
