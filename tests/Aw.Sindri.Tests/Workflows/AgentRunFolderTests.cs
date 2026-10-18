using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The engine's half of the run folder (sandbox spec SB17): created outside the repo, 0700, with the declared inputs written into it before the launch.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentRunFolderTests : IDisposable
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-3/step/1");
    private readonly TempHome _home = new();
    private readonly InMemoryFactStore _facts = new();

    public void Dispose() => _home.Dispose();

    private static AgentStepRun Run(string token = "1.1") => new(WorkflowInstanceId.Of(_address), _address, RunToken.From(token), new StepRef("produce", "step.produce"), WriteScope: false);

    private AgentRunFolder Folder() => new(new WorkflowsHome(_home.Path), _facts);

    private async Task<FactValue> Fact(string text) => (await _facts.Put(Encoding.UTF8.GetBytes(text), TestContext.Current.CancellationToken)).Value;

    [Fact]
    public async Task The_folder_is_made_private_and_holds_each_declared_input_as_a_file()
    {
        var inputs = new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["fact:issue"] = await Fact("The button is broken") };

        var prepared = await Folder().Prepare(Run(), inputs, TestContext.Current.CancellationToken);

        Assert.True(prepared.IsSuccess, prepared.IsError ? prepared.Error.Message : null);
        Expect.Equal(RunFolders.PathFor(_home.Path, Run().Workflow, "produce", Run().Run), prepared.Value);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(prepared.Value));
        }

        Expect.Equal("The button is broken", await File.ReadAllTextAsync(Path.Combine(prepared.Value, "issue"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_rerun_gets_its_own_empty_folder()
    {
        var inputs = new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["fact:issue"] = await Fact("x") };
        var first = (await Folder().Prepare(Run("1.1"), inputs, TestContext.Current.CancellationToken)).Value;
        await File.WriteAllTextAsync(Path.Combine(first, "partial.out"), "killed run", TestContext.Current.CancellationToken);

        var second = (await Folder().Prepare(Run("1.2"), inputs, TestContext.Current.CancellationToken)).Value;

        Expect.True(!string.Equals(first, second, StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(second, "partial.out")));
    }

    [Fact]
    public async Task Preparing_the_same_run_again_changes_nothing()
    {
        var inputs = new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["fact:issue"] = await Fact("x") };

        var first = await Folder().Prepare(Run(), inputs, TestContext.Current.CancellationToken);
        var again = await Folder().Prepare(Run(), inputs, TestContext.Current.CancellationToken);

        Assert.True(again.IsSuccess);
        Expect.Equal(first.Value, again.Value);
    }

    [Fact]
    public async Task An_input_that_cannot_be_read_stops_the_launch()
    {
        var inputs = new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["fact:issue"] = new FactValue("blob:" + new string('d', 64), new string('d', 64), HeadSha: null) };

        var prepared = await Folder().Prepare(Run(), inputs, TestContext.Current.CancellationToken);

        Assert.True(prepared.IsError);
    }
}
