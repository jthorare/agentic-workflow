using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Eval;
using Aw.Judge.Infrastructure.Redaction;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Redaction;

namespace Aw.Judge.Tests.Labels;

// The real redactor runs here, so the contract's secret test checks the shipped patterns too.
[Lane(TestLane.Integration)]
public sealed class SqliteEvalStoreContractTests : EvalStoreContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override ValueTask<IEvalStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEvalStore>(new SqliteEvalStore(_state.Database(), new AwInputRedactor(Redactor.Default), AwJson.CreateOptions(JudgeJsonContext.Default)));

    protected override ValueTask<IEvalStore> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEvalStore>(new SqliteEvalStore(_state.UnopenableDatabase(), new AwInputRedactor(Redactor.Default), AwJson.CreateOptions(JudgeJsonContext.Default)));

    protected override async ValueTask SeedDecision(IEvalStore store, DecidedInput input, DateTimeOffset at, SessionId? session, CancellationToken cancellationToken)
    {
        var record = A.Decision().WithId(input.Id.Value).At(at).Build() with
        {
            Question = input.Question,
            Decision = input.Answer,
            Details = new DecisionDetails { InputJson = input.InputJson, SessionId = session },
        };
        var recorded = await _state.DecisionLog().Record(record, cancellationToken);
        Assert.True(recorded.IsSuccess);
    }

    public void Dispose() => _state.Dispose();
}
