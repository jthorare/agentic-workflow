using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.RerunInstance;

/// <summary>F5 PR B: 'RerunInstance' starts the same recipe version on the same item as the next attempt with the same inputs, and records the address it reruns.</summary>
[Lane(TestLane.Unit)]
public sealed class RerunInstanceServiceTests
{
    private const string Sha = "0000000000000000000000000000000000000000000000000000000000000001";

    private sealed class Contents : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes("the spec " + fact.Sha256[^1])));
    }

    private sealed class CapturingStart(InstanceView answer) : IService<StartInstanceRequest, StartInstanceResponse>
    {
        public List<StartInstanceRequest> Starts { get; } = [];

        public ValueTask<Result<StartInstanceResponse>> Handle(StartInstanceRequest request, CancellationToken cancellationToken)
        {
            Starts.Add(request);
            return ValueTask.FromResult<Result<StartInstanceResponse>>(new StartInstanceResponse(answer, Existing: false, Finished: false));
        }
    }

    private static InstanceView Ended(string name) => StatusCases.View(name) with
    {
        Inputs =
        [
            new InputPresence("fact:spec", "Spec", "text", Required: true, Present: true),
            new InputPresence("fact:stamp", "Stamp", "text", Required: false, Present: true),
        ],
        FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:spec"] = Sha, ["fact:stamp"] = Sha[..^1] + "2" },
    };

    private static async Task<(Result<StartInstanceResponse> Result, CapturingStart Start)> Rerun(InstanceView view)
    {
        var instances = new SomeInstances(view);
        var start = new CapturingStart(view);
        var service = new RerunInstanceService(instances, instances, new Contents(), start);
        return (await service.Handle(new RerunInstanceRequest(view.Id.Value), CancellationToken.None), start);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("unresolved")]
    [InlineData("cancelled")]
    public async Task A_rerun_starts_the_same_recipe_version_on_the_same_item_as_the_next_attempt_and_records_the_old_address(string name)
    {
        var view = Ended(name);

        var (result, start) = await Rerun(view);

        Assert.False(result.IsError);
        var request = Assert.Single(start.Starts);
        Assert.Equal(($"{view.Recipe.Id.Value}@{view.Recipe.Hash.Value}", view.ItemId!.Value.Value, true, view.Address), (request.Recipe, request.Item, request.NextAttempt, request.Again));
    }

    [Fact]
    public async Task A_rerun_repeats_the_required_inputs_with_their_stored_content_and_leaves_a_generated_one_to_be_generated_again()
    {
        var (_, start) = await Rerun(Ended("failed"));

        var facts = Assert.Single(start.Starts).Facts;
        var spec = Assert.Single(facts);
        Assert.Equal(("fact:spec", "spec", "the spec 1"), (spec.Key.Value, spec.Value.Name, Encoding.UTF8.GetString(spec.Value.Content.Span)));
    }

    [Theory]
    [InlineData("done")]
    [InlineData("desk-task")]
    public async Task A_finished_or_live_instance_is_refused_and_nothing_is_started(string name)
    {
        var (result, start) = await Rerun(Ended(name));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(start.Starts);
    }

    [Fact]
    public async Task An_instance_that_does_not_exist_is_not_found()
    {
        var instances = new SomeInstances();
        var service = new RerunInstanceService(instances, instances, new Contents(), new CapturingStart(Ended("failed")));

        var result = await service.Handle(new RerunInstanceRequest("WFI-00000000000000000000000000000000"), CancellationToken.None);

        Assert.True(result.IsError);
    }
}
