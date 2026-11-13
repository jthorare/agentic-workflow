using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Features.SetupSources;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>The first-workflow step is done when hello-sindri has a succeeded run (first-run spec §3.1, step 8); the fact is derived from the workflows and their runs, nothing new is stored.</summary>
[Lane(TestLane.Unit)]
public sealed class FirstWorkflowFactTests : IAsyncDisposable
{
    private static readonly WorkflowSlug _hello = WorkflowSlug.From("hello-sindri");
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");

    private readonly SetupFactsRig _rig = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly ProfileSetupReader _reader;

    public FirstWorkflowFactTests()
    {
        _workflows = new InMemoryWorkflowStore(_rig.Clock);
        _runs = new InMemoryWorkflowRunStore(_rig.Clock);
        _rig.Profiles.Live = SetupFactsRig.ProfileWithRepos();
        _reader = new ProfileSetupReader(_rig.Profiles, new Core.Setup.RepoGitCache(_rig.Inspector, _rig.Clock), new SetupFactsRig.NoProfileFile(), _workflows, _runs);
    }

    public ValueTask DisposeAsync() => _rig.DisposeAsync();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task Seed(WorkflowRunStatus? status)
    {
        _ = (await _workflows.Create(_id, _hello, GraphTemplates.Find("hello-sindri")!.Spec, "service:setup", Token)).Value;
        if (status is { } s)
        {
            var run = new WorkflowRun(WorkflowRunId.Of(_hello, 1, 1), _id, 1, """{"kind":"manual"}""", s, Outputs: null, Error: null, ItemId: null, _rig.Clock.GetUtcNow(), s is WorkflowRunStatus.Running ? null : _rig.Clock.GetUtcNow());
            _ = (await _runs.Insert(run, Token)).Value;
        }
    }

    [Fact]
    public async Task A_box_without_hello_sindri_has_not_run_it()
    {
        var fact = (await _reader.FirstWorkflow(Token)).Value;

        Assert.Equal(new Core.Setup.FirstWorkflowFact(Succeeded: false, Dismissed: false), fact);
    }

    [Fact]
    public async Task A_succeeded_run_of_hello_sindri_is_done()
    {
        await Seed(WorkflowRunStatus.Succeeded);

        var fact = (await _reader.FirstWorkflow(Token)).Value;

        Assert.True(fact.Succeeded);
    }

    [Theory]
    [InlineData(WorkflowRunStatus.Running)]
    [InlineData(WorkflowRunStatus.Failed)]
    [InlineData(WorkflowRunStatus.Cancelled)]
    public async Task A_run_that_did_not_succeed_is_not_done(WorkflowRunStatus status)
    {
        await Seed(status);

        var fact = (await _reader.FirstWorkflow(Token)).Value;

        Assert.False(fact.Succeeded);
    }

    [Fact]
    public async Task A_workflow_that_never_ran_is_not_done()
    {
        await Seed(status: null);

        var fact = (await _reader.FirstWorkflow(Token)).Value;

        Assert.False(fact.Succeeded);
    }
}
