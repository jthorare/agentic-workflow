using System.Globalization;
using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>What the job and service verb tests share: the verbs over a <see cref="JobApi"/>, their output, the two Ctrl-C tokens and a counting fake clock.</summary>
internal sealed class JobRig : IDisposable
{
    internal const string Id = "JOB-0123456789abcdef0123456789abcdef";

    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly CancellationTokenSource _interrupt = new();
    private readonly CancellationTokenSource _again = new();

    public StringWriter Out { get; } = new();

    public StringWriter Error { get; } = new();

    public FollowRig.CountingClock Clock { get; } = new();

    public CancellationToken Interrupted => _interrupt.Token;

    public CancellationToken Again => _again.Token;

    public static JobStartedResponse Started { get; } = new(new CommandAccepted(CommandId.From(new Guid(7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), 10), JobId.From(Id),
        new JobLinks("/sindri/jobs/" + Id, "/sindri/jobs/" + Id + "/cancel", "/sindri/events?after=10&types=job.*"));

    public void Dispose()
    {
        _interrupt.Dispose();
        _again.Dispose();
        Out.Dispose();
        Error.Dispose();
    }

    public static string Body<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    public static string View(JobState state, int rounds, string? summary = null, string? code = null, System.Text.Json.JsonElement? result = null) => Body(new JobView(JobId.From(Id), JobKind.From("RunService"), JobKey.From("heartbeat"),
        Item: null, Repo: null, ServiceName.From("heartbeat"), state, "human:local", KnownInstants.Epoch, state == JobState.Running ? null : KnownInstants.Epoch,
        [.. Enumerable.Range(1, rounds).Select(static n => new JobLine(n, string.Create(CultureInfo.InvariantCulture, $"line {n}"), KnownInstants.Epoch))], rounds, summary, code, result, CancelledBy: null,
        state == JobState.Running ? [ActionId.From("CancelJob")] : [], state == JobState.Running ? "sindri jobs cancel " + Id : null, new AsOf(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false)));

    /// <summary>A job that made model calls (context model routing spec §8.2): its totals, its calls and, when <paramref name="waitingOn"/> is set, the progress line it waits with.</summary>
    public static string ModelView(JobState state, string? waitingOn, IReadOnlyList<string> lines, IReadOnlyList<ContextModelCalled> calls, int count) => Body(new JobView(JobId.From(Id), JobKind.From("BuildContextMap"), JobKey.From("map"),
        Item: null, Repo: null, Service: null, state, "human:local", KnownInstants.Epoch, EndedAt: null,
        [.. lines.Select(static (line, n) => new JobLine(n + 1, line, KnownInstants.Epoch))], lines.Count, Summary: null, Code: null, Result: null, CancelledBy: null,
        [ActionId.From("CancelJob")], "sindri jobs cancel " + Id, new AsOf(10, 10, KnownInstants.Epoch, Rebuilding: false, Stalled: false),
        Models: new ModelTotals(count, FailedCalls: 0, InputTokens: 6000, OutputTokens: 1900, [new ModelAnsweredCount("codex", "gpt-6-luna", 2)], FellBack: 1), WaitingOn: waitingOn, ModelCalls: calls, ModelCallCount: count));

    public static string Progress(int round, string line) => string.Create(CultureInfo.InvariantCulture, $$"""{"commandId":"00000000-0000-0000-0000-000000000007","job":"{{Id}}","round":{{round}},"line":"{{line}}"}""");

    public static string Finished(string outcome, string summary, string? code = null) =>
        $$"""{"commandId":"00000000-0000-0000-0000-000000000007","job":"{{Id}}","outcome":"{{outcome}}","summary":"{{summary}}","code":{{(code is null ? "null" : "\"" + code + "\"")}}}""";

    public static string Cancelled() => $$"""{"commandId":"00000000-0000-0000-0000-000000000008","job":"{{Id}}","by":"human:local"}""";

    public static ControlPlaneClient Client(JobApi api) => new(_probe, SliceHarness.Json,
        CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);

    public Output Output() => new(Out, Error, SliceHarness.Json);

    public JobFollower Follower(JobApi api) => new(Client(api), Output(), Clock);

    public ServiceVerbs Services(JobApi api) => new(Client(api), Output(), Follower(api), (_, _) => Task.FromResult<Kernel.Error?>(null));

    public JobVerbs Jobs(JobApi api) => new(Client(api), Output(), (_, _) => Task.FromResult<Kernel.Error?>(null), Clock);

    public Task Interrupt() => _interrupt.CancelAsync();

    public Task SecondInterrupt() => _again.CancelAsync();

    public static Task<int> Finished(Task<int> run) => run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

    /// <summary>Moves the fake clock by <paramref name="step"/> each time the verb sets a new timer, until <paramref name="run"/> ends: no wall-clock wait, and a timer set late still fires.</summary>
    /// <param name="step">How far to move the clock for each timer.</param>
    /// <param name="run">The verb's run.</param>
    /// <returns>A task that completes when the run ended.</returns>
    public async Task AdvanceOnTimers(TimeSpan step, Task run)
    {
        for (var armed = 0; ; armed++)
        {
            _ = await Task.WhenAny(run, Clock.TimersReached(armed + 1)).WaitAsync(TimeSpan.FromSeconds(15), TimeProvider.System, TestContext.Current.CancellationToken);
            if (run.IsCompleted)
            {
                return;
            }

            Clock.Advance(step);
        }
    }

    public Task<int> RunServices(JobApi api, params string[] args) => Task.Run(() => Services(api).Run(args, Interrupted, Again));

    public Task<int> RunJobs(JobApi api, params string[] args) => Task.Run(() => Jobs(api).Run(args, Interrupted));

    public static Reply Refused(HttpStatusCode status, string code, string detail) => new(status, string.Create(CultureInfo.InvariantCulture, $$"""{"title":"{{code}}","status":{{(int)status}},"detail":"{{detail}}"}"""));
}
