using System.Diagnostics;
using System.Reflection;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// A type that carries Temporal's [Workflow] attribute runs on the workflow's own deterministic scheduler, so it calls nothing that runs on the thread pool, reads the
/// wall clock or generates a random value (issue 2432). The types are found by the attribute type, never by folder or namespace. A partial type is one Type, so its
/// parts without the attribute are checked with it, and so are the types nested in it (closures, state machines, helpers). Read from the IL.
/// </summary>
public static class WorkflowSchedulerRules
{
    /// <summary>A workflow type calls a scheduler-escaping API.</summary>
    public const string Id = "ARCH-WF-001";

    private const string Sleep = "use await Workflow.DelayAsync(delay)";
    private const string Utc = "use Workflow.UtcNow";
    private const string WorkflowAttribute = "Temporalio.Workflows.WorkflowAttribute";

    private static readonly Entry _configureAwaitFalse = new(typeof(Task), "ConfigureAwait", "ConfigureAwait(false)",
        "use ConfigureAwait(true) (CA2007 wants an explicit one): false resumes the continuation on the thread pool, outside the workflow scheduler");
    private static readonly Func<MethodBase, bool> _timeSpanFirst = static m => FirstParameterIs(m, typeof(TimeSpan));
    private static readonly Func<MethodBase, bool> _intFirst = static m => FirstParameterIs(m, typeof(int));
    private static readonly IReadOnlyList<Entry> _banned = Entries();

    /// <summary>The types that carry Temporal's [Workflow] attribute.</summary>
    /// <param name="types">Every type of the assemblies in scope.</param>
    /// <returns>The workflow types.</returns>
    public static IEnumerable<Type> WorkflowTypes(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return types.Where(static t => t.GetCustomAttributesData().Any(static a => string.Equals(a.AttributeType.FullName, WorkflowAttribute, StringComparison.Ordinal)));
    }

    /// <summary>Checks the types.</summary>
    /// <param name="types">Every type of the assemblies in scope.</param>
    /// <returns>One violation per member and banned call.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var workflows = WorkflowTypes(types).ToList();
        return workflows.Where(t => !NestedIn(t, workflows)).SelectMany(Violations);
    }

    private static bool NestedIn(Type type, IReadOnlyCollection<Type> workflows)
    {
        for (var owner = type.DeclaringType; owner is not null; owner = owner.DeclaringType)
        {
            if (workflows.Contains(owner))
            {
                return true;   // read through its owner
            }
        }

        return false;
    }

    private static IEnumerable<Violation> Violations(Type workflow) =>
        IlScan.Calls(workflow)
            .Select(c => (Member: MemberName(c.Caller, workflow), Ban: Find(c)))
            .Where(static x => x.Ban is not null)
            .Distinct()
            .Select(x => new Violation(Id, $"{x.Member} ({workflow.FullName}) calls {x.Ban!.Api}", x.Ban.Fix));

    private static Entry? Find(IlCall call)
    {
        var method = call.Callee;
        if (string.Equals(method.Name, "ConfigureAwait", StringComparison.Ordinal))
        {
            return call.ConstantFalseArgument ? _configureAwaitFalse : null;
        }

        var declaring = method.DeclaringType;
        var definition = declaring is { IsGenericType: true } ? declaring.GetGenericTypeDefinition() : declaring;
        return _banned.FirstOrDefault(e => e.Declaring == definition
            && (e.Member is null || string.Equals(e.Member, method.Name, StringComparison.Ordinal))
            && (e.When is null || e.When(method)));
    }

    // The member a person wrote: a state machine or a lambda is named after its method, a nested type keeps its name in front.
    private static string MemberName(MethodBase caller, Type workflow)
    {
        var parts = new List<string>();
        for (var owner = caller.DeclaringType; owner is not null && owner != workflow; owner = owner.DeclaringType)
        {
            if (!owner.Name.StartsWith('<'))
            {
                parts.Insert(0, owner.Name);
            }
        }

        parts.Add(Between(caller.Name) ?? Between(caller.DeclaringType?.Name) ?? caller.Name);
        return string.Join('.', parts);
    }

    // "<RunAsync>d__3" -> "RunAsync", "<Outer>b__0_0" -> "Outer"; null when the name has no such part.
    private static string? Between(string? name)
    {
        if (name is null || !name.StartsWith('<'))
        {
            return null;
        }

        var end = name.IndexOf('>', StringComparison.Ordinal);
        return end > 1 ? name[1..end] : null;
    }

    private static bool FirstParameterIs(MethodBase method, Type type) =>
        method.GetParameters() is [{ } first, ..] && first.ParameterType == type;

    private static List<Entry> Entries() => [.. Cancellation(), .. Tasks(), .. Clocks(), .. Others()];

    private static Entry[] Cancellation()
    {
        const string cancel = "call Cancel() instead: CancelAsync runs the callbacks on the thread pool, outside the workflow scheduler (MA0042 is suppressed for it, see .agents/rules/csharp.md)";
        const string timer = "cancel after a Workflow timer: await Workflow.DelayAsync(delay), then call Cancel(); the token source's own timer fires on the thread pool";
        return
        [
            new(typeof(CancellationTokenSource), "CancelAsync", "CancellationTokenSource.CancelAsync", cancel),
            new(typeof(CancellationTokenSource), "CancelAfter", "CancellationTokenSource.CancelAfter", timer),
            new(typeof(CancellationTokenSource), ".ctor", "CancellationTokenSource(TimeSpan)", timer, _timeSpanFirst),
            new(typeof(CancellationTokenSource), ".ctor", "CancellationTokenSource(int)", timer, _intFirst),
        ];
    }

    private static Entry[] Tasks()
    {
        const string task = "run it with Workflow.RunTaskAsync, or as an activity when it does real work";
        const string wait = "use Workflow.WaitConditionAsync(condition, timeout), or Workflow.WhenAnyAsync(task, Workflow.DelayAsync(timeout))";
        return
        [
            new(typeof(Task), "Run", "Task.Run", task),
            new(typeof(Task), "Delay", "Task.Delay", Sleep),
            new(typeof(Task), "WhenAny", "Task.WhenAny", "use Workflow.WhenAnyAsync(...)"),
            new(typeof(Task), "WhenAll", "Task.WhenAll", "use Workflow.WhenAllAsync(...)"),
            new(typeof(Task), "Yield", "Task.Yield", "drop it and await a Workflow.* call (for example Workflow.DelayAsync) where the workflow has to wait"),
            new(typeof(TaskFactory), "StartNew", "Task.Factory.StartNew", task),
            new(typeof(TaskFactory<>), "StartNew", "Task.Factory.StartNew", task),
            new(typeof(Task), "WaitAsync", "Task.WaitAsync(TimeSpan)", wait, _timeSpanFirst),
            new(typeof(Task<>), "WaitAsync", "Task.WaitAsync(TimeSpan)", wait, _timeSpanFirst),
        ];
    }

    private static Entry[] Clocks() =>
    [
        new(typeof(DateTime), "get_Now", "DateTime.Now", Utc),
        new(typeof(DateTime), "get_UtcNow", "DateTime.UtcNow", Utc),
        new(typeof(DateTime), "get_Today", "DateTime.Today", Utc + ".Date"),
        new(typeof(DateTimeOffset), "get_Now", "DateTimeOffset.Now", Utc),
        new(typeof(DateTimeOffset), "get_UtcNow", "DateTimeOffset.UtcNow", Utc),
        new(typeof(Stopwatch), Member: null, Api: "Stopwatch", Fix: "measure with the difference of two Workflow.UtcNow reads"),
        new(typeof(TimeProvider), "get_System", "TimeProvider.System", "use Workflow.UtcNow to read the time and Workflow.DelayAsync to wait"),
        new(typeof(Environment), "get_TickCount", "Environment.TickCount", Utc),
        new(typeof(Environment), "get_TickCount64", "Environment.TickCount", Utc),
    ];

    private static Entry[] Others()
    {
        const string parallel = "start each piece with Workflow.RunTaskAsync and join them with Workflow.WhenAllAsync";
        return
        [
            new(typeof(Guid), "NewGuid", "Guid.NewGuid", "use Workflow.NewGuid()"),
            new(typeof(Random), ".ctor", "new Random", "use Workflow.Random"),
            new(typeof(Random), "get_Shared", "Random.Shared", "use Workflow.Random"),
            new(typeof(Thread), "Sleep", "Thread.Sleep", Sleep),
            new(typeof(Parallel), "For", "Parallel.For", parallel),
            new(typeof(Parallel), "ForEach", "Parallel.ForEach", parallel),
            new(typeof(Parallel), "ForAsync", "Parallel.ForAsync", parallel),
            new(typeof(Parallel), "ForEachAsync", "Parallel.ForEachAsync", parallel),
            new(typeof(Parallel), "Invoke", "Parallel.Invoke", parallel),
            new(typeof(System.Threading.Timer), ".ctor", "System.Threading.Timer", "loop on await Workflow.DelayAsync(period)"),
        ];
    }

    private sealed record Entry(Type Declaring, string? Member, string Api, string Fix, Func<MethodBase, bool>? When = null);
}
