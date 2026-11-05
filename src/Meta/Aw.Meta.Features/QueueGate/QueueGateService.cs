using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.QueueGate;

/// <summary>
/// Gates the next pull requests of the merge queue as a train: the first gates on origin/main, each later one on origin/main
/// with the heads ahead of it merged in order, in its own gate tree. A pull request that conflicts is dropped from the round;
/// all the gates share one pool of heavy slots, ordered by train position; when one gate fails, the gates stacked above it
/// that are still running are cancelled (their own process trees are killed) and not recorded.
/// </summary>
/// <param name="queue">Chooses the pull requests and loads the team config.</param>
/// <param name="workspace">Builds the trees.</param>
/// <param name="steps">Runs a step.</param>
/// <param name="gates">Stores the runs.</param>
/// <param name="values">Holds the train's state for the status.</param>
/// <param name="reservation">Computes the heavy-slot reservation from the merge-queue depth before the train.</param>
/// <param name="clock">Dates the runs.</param>
internal sealed class QueueGateService(TrainSelection queue, GateWorkspace workspace, GateStepRunner steps, IGateStore gates, IKeyValueStore values, HeavyReservation reservation, TimeProvider clock)
    : IService<QueueGateRequest, QueueGateResponse>
{
    public async ValueTask<Result<QueueGateResponse>> Handle(QueueGateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var chosen = await queue.Choose(request.Prs, cancellationToken).ConfigureAwait(false);
        if (chosen.IsError)
        {
            return chosen.Error;
        }

        var team = chosen.Value.Team;
        var reserved = await reservation.Apply(team, cancellationToken).ConfigureAwait(false);
        var built = await Build(team, chosen.Value.Prs, Math.Max(1, request.Max ?? team.Heavy.Cap), cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        using var board = new TrainBoard(values, built.Value.Main, "TRAIN pid " + request.Pid.ToString(CultureInfo.InvariantCulture) + " started " + Stamp());
        GateRecord?[] recorded;
        try
        {
            foreach (var entry in built.Value.Left)
            {
                await board.Set(entry).ConfigureAwait(false);
            }

            recorded = await Run(team, built.Value, board, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // However the train ends (an exception, a signal), the board says so: a board with a start and no end is a train that died.
            await board.Publish("TRAIN ended " + Stamp()).ConfigureAwait(false);
        }

        return new QueueGateResponse(
            built.Value.Main,
            [.. board.Entries.OrderBy(e => e.Position == 0 ? int.MaxValue : e.Position).ThenBy(e => e.Pr)],
            [.. recorded.OfType<GateRecord>()],
            reserved.IsSuccess ? reserved.Value : null);
    }

    private string Stamp() => clock.GetUtcNow().ToString("u", CultureInfo.InvariantCulture);

    // Builds the trees one after the other (each stacks on the ones before it): the gates to run, and the pull requests left out.
    private async ValueTask<Result<Built>> Build(TeamConfig team, IReadOnlyList<int> candidates, int size, CancellationToken cancellationToken)
    {
        var gating = new List<Planned>();
        var left = new List<TrainEntry>();
        string? main = null;
        foreach (var pr in candidates.TakeWhile(_ => gating.Count < size))
        {
            var stack = gating.Select(g => new StackedPr(g.Pr, g.Head)).ToList();
            var prepared = await workspace.Prepare(team, pr, listChanged: true, new GateBase(main, stack), cancellationToken).ConfigureAwait(false);
            if (prepared.IsError)
            {
                left.Add(new TrainEntry(0, pr, TrainText.BaseOf(stack), TrainState.Error, prepared.Error.Message.ReplaceLineEndings(" ")));
                continue;
            }

            main ??= prepared.Value.Main;
            if (prepared.Value.Pr.IsDraft)
            {
                left.Add(new TrainEntry(0, pr, TrainText.BaseOf(stack), TrainState.Skipped, "draft"));
            }
            else if (!prepared.Value.Merged)
            {
                left.Add(new TrainEntry(0, pr, TrainText.BaseOf(stack), TrainState.Dropped, Conflict(prepared.Value, main)));
            }
            else
            {
                var planned = await Plan(team, pr, stack, prepared.Value, cancellationToken).ConfigureAwait(false);
                if (planned.IsError)
                {
                    return planned.Error;
                }

                gating.Add(planned.Value);
            }
        }

        return new Built(main ?? string.Empty, gating, left);
    }

    private static string Conflict(PreparedGate prepared, string main) =>
        prepared.ConflictsWith is { } with ? string.Create(CultureInfo.InvariantCulture, $"conflicts with #{with.Pr} at {Short(with.Head)}") : $"conflicts with main at {Short(main)}";

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;

    private async ValueTask<Result<Planned>> Plan(TeamConfig team, int pr, List<StackedPr> stack, PreparedGate prepared, CancellationToken cancellationToken)
    {
        var scopes = ScopePlanner.Plan(prepared.Changed, team.Gate);
        var graph = GateGraphs.Build(scopes, team.Gate);
        var latest = await gates.Latest(pr, cancellationToken).ConfigureAwait(false);
        if (graph.IsError || latest.IsError)
        {
            return graph.IsError ? graph.Error : latest.Error;
        }

        var run = await workspace.NextRun(pr, latest.Value?.Run ?? 0, cancellationToken).ConfigureAwait(false);
        return run.IsError
            ? run.Error
            : new Planned(stack.Count + 1, pr, prepared.Pr.Head, stack, prepared, scopes, graph.Value, run.Value);
    }

    private async Task<GateRecord?[]> Run(TeamConfig team, Built built, TrainBoard board, CancellationToken cancellationToken)
    {
        using var pool = new Train(built.Gating.Count, Math.Max(1, team.Gate.ParallelSteps ?? team.Heavy.Cap), cancellationToken);
        foreach (var gate in built.Gating)
        {
            await board.Set(new TrainEntry(gate.Position, gate.Pr, TrainText.BaseOf(gate.Stack), TrainState.Gating)).ConfigureAwait(false);
        }

        return await Task.WhenAll(built.Gating.Select(gate => RunOne(gate, pool, board))).ConfigureAwait(false);
    }

    private async Task<GateRecord?> RunOne(Planned gate, Train train, TrainBoard board)
    {
        var token = train.Tokens[gate.Position - 1].Token;
        var @base = TrainText.BaseOf(gate.Stack);
        try
        {
            var ran = await StepScheduler.Run(
                gate.Graph,
                train.Slots,
                gate.Position,
                async (step, stepToken) =>
                {
                    var done = await steps.Run(step, gate.Prepared.Tree, workspace.LogPath(gate.Pr, gate.Run, step.Name), train.Limit, stepToken).ConfigureAwait(false);
                    // Only a red step stops the gates above; an error (a step that could not start, the wrapper's lock timeout) says nothing about the code.
                    if (done.IsSuccess && done.Value.Verdict != GateVerdict.Pass)
                    {
                        await train.CancelAbove(gate.Position).ConfigureAwait(false);
                    }

                    return done;
                },
                token).ConfigureAwait(false);
            if (ran.IsError)
            {
                await board.Set(new TrainEntry(gate.Position, gate.Pr, @base, TrainState.Error, ran.Error.Message.ReplaceLineEndings(" "))).ConfigureAwait(false);
                return null;
            }

            return await Record(gate, ran.Value, board).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || train.Interrupted)
        {
            await board.Set(new TrainEntry(gate.Position, gate.Pr, @base, TrainState.Cancelled, train.Interrupted ? "interrupted; stays queued" : "a gate below it failed; stays queued")).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever a gate throws, the train still ends with a board and the gates that did finish recorded.
            await board.Set(new TrainEntry(gate.Position, gate.Pr, @base, TrainState.Error, exception.Message.ReplaceLineEndings(" "))).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<GateRecord?> Record(Planned gate, IReadOnlyList<GateStep> ran, TrainBoard board)
    {
        var @base = TrainText.BaseOf(gate.Stack);
        var verdict = ran.All(s => s.Verdict == GateVerdict.Pass) ? GateVerdict.Pass : GateVerdict.Fail;
        var record = new GateRecord(gate.Pr, gate.Head, gate.Prepared.Main, gate.Scopes, ran, verdict, clock.GetUtcNow()) { Run = gate.Run, Tree = gate.Prepared.TreeSha, Stack = gate.Stack };
        var saved = await gates.Save(record, CancellationToken.None).ConfigureAwait(false);
        await board.Set(saved.IsError
            ? new TrainEntry(gate.Position, gate.Pr, @base, TrainState.Error, saved.Error.Message.ReplaceLineEndings(" "))
            : new TrainEntry(gate.Position, gate.Pr, @base, verdict == GateVerdict.Pass ? TrainState.Pass : TrainState.Fail)).ConfigureAwait(false);
        return saved.IsError ? null : saved.Value;
    }

    private sealed record Planned(int Position, int Pr, string Head, IReadOnlyList<StackedPr> Stack, PreparedGate Prepared, IReadOnlyList<string> Scopes, StepGraph Graph, int Run);

    private sealed record Built(string Main, IReadOnlyList<Planned> Gating, IReadOnlyList<TrainEntry> Left);

    // The heavy slots every gate shares and one cancellation source per gate, so a failure can stop the gates above it.
    private sealed class Train : IDisposable
    {
        private readonly CancellationToken _caller;

        internal Train(int gates, int limit, CancellationToken caller)
        {
            _caller = caller;
            Limit = limit;
            Slots = new SlotPool(limit);
            Tokens = [.. Enumerable.Range(0, gates).Select(_ => CancellationTokenSource.CreateLinkedTokenSource(caller))];
        }

        internal int Limit { get; }

        internal bool Interrupted => _caller.IsCancellationRequested;

        internal SlotPool Slots { get; }

        internal CancellationTokenSource[] Tokens { get; }

        internal async Task CancelAbove(int position)
        {
            foreach (var source in Tokens.Skip(position))
            {
                await source.CancelAsync().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            foreach (var source in Tokens)
            {
                source.Dispose();
            }
        }
    }
}
