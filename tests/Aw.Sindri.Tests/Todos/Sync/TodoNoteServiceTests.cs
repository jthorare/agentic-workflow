using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features;
using Aw.Sindri.Features.SyncTodoNote;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>The 'todo-note' built-in service (todo spec §4.5 "Reading"): registered with a 24 h cadence beside its scheduled service, and a run is one note pass.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteServiceTests
{
    [Fact]
    public void Todo_note_is_a_registered_daily_built_in_with_its_scheduled_service()
    {
        var services = new ServiceCollection().AddTodoSlices();

        var builtIn = Assert.Single(services.Where(static d => d.ServiceType == typeof(BuiltInService)).Select(static d => (BuiltInService)d.ImplementationInstance!));
        Assert.Equal("todo-note", builtIn.Name.Value);
        Assert.Equal(TimeSpan.FromHours(24), builtIn.Cadence.Interval);
        Assert.Contains(services, static d => d.ServiceType == typeof(IScheduledService) && d.ImplementationType == typeof(TodoNoteService));
    }

    [Fact]
    public async Task A_run_is_one_pass_and_a_quiet_result()
    {
        var sync = new CountingSync();
        var slices = new SliceHarness();
        var grant = await slices.HoldLease();

        var ran = await new TodoNoteService(sync).Run(new ScheduledRun(grant, slices.Clock.GetUtcNow()), TestContext.Current.CancellationToken);

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Quiet, ran.Value);
        Assert.Equal(1, sync.Passes);
    }

    private sealed class CountingSync : ITodoNoteSync
    {
        internal int Passes { get; private set; }

        public TodoNoteStatus Status => TodoNoteStatus.NotSet;

        public ValueTask<TodoNoteStatus> Pass(CancellationToken cancellationToken)
        {
            Passes++;
            return ValueTask.FromResult(TodoNoteStatus.NotSet);
        }

        public ValueTask<Result<TodoNoteImport>> Import(TodoNoteImportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
