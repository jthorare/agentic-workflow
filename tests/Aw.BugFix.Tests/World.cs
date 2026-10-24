using Aw.BugFix.Core;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Features;
using Aw.BugFix.Features.AdvanceInvestigate;
using Aw.BugFix.Features.AdvanceReproduce;
using Aw.BugFix.Features.Init;
using Aw.BugFix.Features.RecordCandidate;
using Aw.BugFix.Features.RecordRun;
using Aw.BugFix.Features.RunTest;
using Aw.BugFix.Features.StartAttempt;
using Aw.BugFix.Host;
using Aw.BugFix.Testing;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.BugFix.Tests;

/// <summary>One bug fix over the in-memory fakes: a repository at /repo with a committed failing check, a ticket and a handoff.</summary>
internal sealed class World : IDisposable
{
    public const string State = "/state/eng-1";
    public const string Repo = "/repo";
    public const string TicketFile = "/state/ticket.json";
    public const string HandoffFile = "/state/handoff.md";
    public const string Check = "tests/bug.test.sh";
    public const string Worktree = "/wt/a1";

    private readonly ServiceProvider _provider;

    public World()
    {
        Ui = new ScriptedUiEvidence(Repos);
        Base = Repos.Init(Repo, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/app.ts"] = "drop cookie\n",
            [Check] = "exit 1\n",
            ["package.json"] = "{}\n",
        });
        Repos.Write(TicketFile, A.TicketJson());
        Repos.Write(HandoffFile, A.Handoff());
        _provider = new ServiceCollection()
            .AddSingleton<IGit>(Repos)
            .AddSingleton<IFiles>(Repos)
            .AddSingleton<IStateStore>(Store)
            .AddSingleton<IRunLedger>(Ledger)
            .AddSingleton<ICheckRunner>(Runner)
            .AddSingleton<IJudge>(Judge)
            .AddSingleton<IUiEvidence>(Ui)
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton(AwJson.CreateOptions(BugFixJsonContext.Default))
            .AddBugFixCore()
            .AddBugFixFeatures()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public InMemoryRepos Repos { get; } = new();

    public InMemoryStateStore Store { get; } = new();

    public InMemoryRunLedger Ledger { get; } = new();

    public ScriptedCheckRunner Runner { get; } = new ScriptedCheckRunner().Exits("bash", 1);

    public ScriptedJudge Judge { get; } = new();

    public ScriptedUiEvidence Ui { get; }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Gets the commit with the committed check: the baseline.</summary>
    public string Base { get; }

    public BugFixState Now => Store.Load(State).Value;

    public static string[] TestCommand => ["bash", Check];

    public async Task<Result<TResponse>> Run<TRequest, TResponse>(TRequest request)
        where TRequest : notnull
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<TRequest, TResponse>>().Handle(request, CancellationToken.None);
    }

    public Task<Result<InitResponse>> Init() => Run<InitRequest, InitResponse>(new InitRequest(State, TicketFile));

    public async Task Investigated()
    {
        _ = Ok(await Init());
        _ = Ok(await Run<AdvanceInvestigateRequest, AdvanceInvestigateResponse>(new AdvanceInvestigateRequest(State, HandoffFile)));
    }

    public Task<Result<RunTestResponse>> TestRun(string directory = Repo) =>
        Run<RunTestRequest, RunTestResponse>(new RunTestRequest(State, Check, directory, TestCommand));

    public async Task Reproduced()
    {
        await Investigated();
        var run = Ok(await TestRun());
        _ = Ok(await Run<AdvanceReproduceRequest, AdvanceReproduceResponse>(new AdvanceReproduceRequest(State, run.Evidence, Check, Repo)));
    }

    /// <summary>A worktree on <paramref name="branch"/> from the baseline with <paramref name="changes"/> committed (the fix by default).</summary>
    public string Fix(string branch = "bugfix/eng-1-a1", string worktree = Worktree, Dictionary<string, string>? changes = null)
    {
        Repos.AddWorktree(worktree, branch, Store.Load(State).Match(static s => s.Baseline?.Commit, static _ => null) ?? Base);
        foreach (var (path, text) in changes ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["src/app.ts"] = "keep cookie\n" })
        {
            Repos.Write($"{worktree}/{path}", text);
        }

        return Repos.Commit(worktree);
    }

    public Task<Result<StartAttemptResponse>> Start(FixMode mode = FixMode.A) =>
        Run<StartAttemptRequest, StartAttemptResponse>(new StartAttemptRequest(State, mode));

    public Task<Result<RecordCandidateResponse>> Record(string branch = "bugfix/eng-1-a1", string worktree = Worktree, int? hypothesis = null, bool allowTestChanges = false, string? reason = null) =>
        Run<RecordCandidateRequest, RecordCandidateResponse>(new RecordCandidateRequest(State, branch, worktree, hypothesis, allowTestChanges, reason));

    /// <summary>Reproduced, attempt 1 in mode A, the fix recorded as c1.</summary>
    public async Task<CandidateId> Candidate()
    {
        await Reproduced();
        _ = Ok(await Start());
        _ = Fix();
        return Ok(await Record()).Candidate;
    }

    /// <summary>A recorded run of the check against c1 with the given exit code.</summary>
    public async Task<Result<RecordRunResponse>> RunAgainst(CandidateId candidate, int exitCode = 0)
    {
        _ = Runner.Exits("bash", exitCode);
        var run = Ok(await TestRun(Worktree));
        return await Run<RecordRunRequest, RecordRunResponse>(new RecordRunRequest(State, candidate, run.Evidence));
    }

    public static T Ok<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsError ? $"{result.Error.Code}: {result.Error.Message}" : string.Empty);
        return result.Value;
    }

    public static Error Refused<T>(Result<T> result, ErrorDefinition expected)
    {
        Assert.True(result.IsError, "expected a refusal, got success");
        Assert.Equal(expected.Code, result.Error.Code);
        return result.Error;
    }

    public void Dispose() => _provider.Dispose();
}
