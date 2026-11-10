using Aw.Architecture.Tests.Rules;
using Aw.Architecture.Tests.WorkflowFixtures;

namespace Aw.Architecture.Tests;

/// <summary>ARCH-WF-001 (issue 2432): a Temporal workflow type never calls an API that runs outside the workflow's scheduler.</summary>
[Trait("Lane", "integration")]
public sealed class WorkflowSchedulerTests
{
    private static readonly string[] _realWorkflows = ["GraphWorkflow", "GraphIterationWorkflow"];

    [Fact]
    public void No_workflow_type_in_the_real_tree_calls_a_scheduler_escaping_API() =>
        Verify.None(WorkflowSchedulerRules.Check(RealTypes()));

    [Fact]
    public void The_real_scan_finds_the_workflow_types_by_their_attribute()
    {
        var found = WorkflowSchedulerRules.WorkflowTypes(RealTypes()).Select(static t => t.Name).ToList();
        Assert.All(_realWorkflows, name => Assert.Contains(found, n => string.Equals(n, name, StringComparison.Ordinal)));
    }

    [Fact]
    public void Each_banned_call_in_a_workflow_type_is_reported_with_its_replacement()
    {
        string[] expected =
        [
            Planted("CancelAsyncCall", "CancellationTokenSource.CancelAsync"),
            Planted("CancelAfterCall", "CancellationTokenSource.CancelAfter"),
            Planted("TimeSpanCtor", "CancellationTokenSource(TimeSpan)"),
            Planted("IntCtor", "CancellationTokenSource(int)"),
            Planted("TaskRunCall", "Task.Run"),
            Planted("TaskDelayCall", "Task.Delay"),
            Planted("WhenAnyCall", "Task.WhenAny"),
            Planted("WhenAllCall", "Task.WhenAll"),
            Planted("ConfigureAwaitFalse", "ConfigureAwait(false)"),
            Planted("LambdaDelay", "Task.Delay"),
            Planted("TaskYield", "Task.Yield"),
            Planted("StartNewCall", "Task.Factory.StartNew"),
            Planted("StartNewOfT", "Task.Factory.StartNew"),          // TaskFactory<T>.StartNew
            Planted("WaitAsyncTimeout", "Task.WaitAsync(TimeSpan)"),
            Planted("WaitAsyncTimeoutOfT", "Task.WaitAsync(TimeSpan)"), // Task<T>.WaitAsync
            Planted("DateTimeNow", "DateTime.Now"),
            Planted("DateTimeUtcNow", "DateTime.UtcNow"),
            Planted("DateTimeToday", "DateTime.Today"),
            Planted("DateTimeOffsetNow", "DateTimeOffset.Now"),
            Planted("DateTimeOffsetUtcNow", "DateTimeOffset.UtcNow"),
            Planted("NewGuidCall", "Guid.NewGuid"),
            Planted("NewRandom", "new Random"),
            Planted("SharedRandom", "Random.Shared"),
            Planted("SleepCall", "Thread.Sleep"),
            Planted("StopwatchCall", "Stopwatch"),
            Planted("SystemTimeProvider", "TimeProvider.System"),
            Planted("TickCount", "Environment.TickCount"),
            Planted("TickCount64", "Environment.TickCount"),
            Planted("ParallelFor", "Parallel.For"),
            Planted("ParallelForEach", "Parallel.ForEach"),
            Planted("ParallelForAsync", "Parallel.ForAsync"),
            Planted("ParallelForEachAsync", "Parallel.ForEachAsync"),
            Planted("ParallelInvoke", "Parallel.Invoke"),
            Planted("ThreadingTimer", "System.Threading.Timer"),
            Planted("Helper.Wait", "Task.Delay"),   // a type nested in the workflow type
            Planted("InPartialPart", "Task.Delay"), // the part of the workflow type that carries no [Workflow]
        ];
        var violations = WorkflowSchedulerRules.Check([typeof(PlantedWorkflow)]).ToList();
        Verify.Exactly(violations, WorkflowSchedulerRules.Id, expected);
        var fixes = violations.Select(static v => v.Fix).ToList();
        foreach (var replacement in new[] { "Workflow.DelayAsync", "Workflow.WhenAnyAsync", "Workflow.WhenAllAsync", "Workflow.UtcNow", "Workflow.NewGuid", "Workflow.Random", "ConfigureAwait(true)" })
        {
            Assert.Contains(fixes, f => f.Contains(replacement, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_Workflow_forms_ConfigureAwait_true_and_a_non_workflow_type_are_not_reported() =>
        Verify.None(WorkflowSchedulerRules.Check([typeof(CleanWorkflow), typeof(NotAWorkflow)]));

    [Fact]
    public void A_partial_part_without_the_attribute_is_checked_with_its_type() =>
        Assert.Contains(WorkflowSchedulerRules.WorkflowTypes([typeof(PlantedWorkflow)]), static t => t == typeof(PlantedWorkflow));

    private static IEnumerable<Type> RealTypes() => Production.Assemblies.SelectMany(static a => a.GetTypes());

    private static string Planted(string member, string api) => $"{member} ({typeof(PlantedWorkflow).FullName}) calls {api}";
}
