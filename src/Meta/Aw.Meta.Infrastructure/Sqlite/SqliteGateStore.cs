using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Meta.Core;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IGateStore over meta.db (the gates, gate_steps and merges tables).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteGateStore(MetaDatabase database) : IGateStore
{
    public ValueTask<Result<GateRecord>> Save(GateRecord gate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        return database.Write(
            async work =>
            {
                var next = await SqliteQueries.One(work, GateSql.NextRun(gate.Pr, gate.Run), new RowMap<int>(r => checked((int)r.GetInt64(0))), cancellationToken).ConfigureAwait(false);
                if (next.IsError)
                {
                    return next.Error;
                }

                var stored = gate with { Run = next.Value };
                foreach (var statement in GateSql.Insert(stored))
                {
                    var changed = await SqliteQueries.Execute(work, statement, cancellationToken).ConfigureAwait(false);
                    if (changed.IsError)
                    {
                        return changed.Error;
                    }
                }

                return Results.Success(stored);
            },
            cancellationToken);
    }

    public async ValueTask<Result<GateRecord?>> Latest(int pr, CancellationToken cancellationToken)
    {
        var heads = await database.Query(GateSql.Latest(pr), GateSql.ReadHead, cancellationToken).ConfigureAwait(false);
        if (heads.IsError || heads.Value.Count == 0)
        {
            return heads.IsError ? heads.Error : Results.Success((GateRecord?)null);
        }

        var full = await WithSteps(heads.Value[0], cancellationToken).ConfigureAwait(false);
        return full.IsError ? full.Error : Results.Success<GateRecord?>(full.Value);
    }

    public async ValueTask<Result<IReadOnlyList<GateRecord>>> History(int pr, CancellationToken cancellationToken)
    {
        var heads = await database.Query(GateSql.All(pr), GateSql.ReadHead, cancellationToken).ConfigureAwait(false);
        if (heads.IsError)
        {
            return heads.Error;
        }

        var runs = new List<GateRecord>();
        foreach (var head in heads.Value)
        {
            var full = await WithSteps(head, cancellationToken).ConfigureAwait(false);
            if (full.IsError)
            {
                return full.Error;
            }

            runs.Add(full.Value);
        }

        return runs;
    }

    public ValueTask<Result<Unit>> Classify(int pr, int run, string step, TestVerdict verdict, CancellationToken cancellationToken) =>
        database.Write(
            async work =>
            {
                var failed = await SqliteQueries.One(work, GateSql.HasFailedStep(pr, run, step), new RowMap<int>(r => checked((int)r.GetInt64(0))), cancellationToken).ConfigureAwait(false);
                if (failed.IsError || failed.Value == 0)
                {
                    return failed.IsError ? failed.Error : MetaErrors.GateStepNotFound.With(string.Create(CultureInfo.InvariantCulture, $"no failed step {step} in gate {pr}/{run}"));
                }

                var stored = await SqliteQueries.Execute(work, GateSql.Classify(pr, run, step, verdict), cancellationToken).ConfigureAwait(false);
                return stored.IsError ? stored.Error : Results.Ok();
            },
            cancellationToken);

    public async ValueTask<Result<MergeRecord?>> FindMerge(int pr, CancellationToken cancellationToken)
    {
        var rows = await database.Query(GateSql.MergeOf(pr), GateSql.ReadMerge, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    public ValueTask<Result<Unit>> RecordMerge(MergeRecord merge, CancellationToken cancellationToken) =>
        database.Run(GateSql.InsertMerge(merge), cancellationToken);

    public async ValueTask<Result<MergeRecord?>> LatestMerge(CancellationToken cancellationToken)
    {
        var rows = await database.Query(GateSql.LatestMerge, GateSql.ReadMerge, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    private async ValueTask<Result<GateRecord>> WithSteps(GateRecord head, CancellationToken cancellationToken)
    {
        var steps = await database.Query(GateSql.Steps(head.Pr, head.Run), GateSql.ReadStep, cancellationToken).ConfigureAwait(false);
        var reruns = steps.IsError ? Results.Failure<IReadOnlyList<(string Step, TestVerdict Verdict)>>(steps.Error) : await database.Query(GateSql.Reruns(head.Pr, head.Run), GateSql.ReadRerun, cancellationToken).ConfigureAwait(false);
        return steps.IsError || reruns.IsError
            ? steps.IsError ? steps.Error : reruns.Error
            : head with { Steps = [.. steps.Value.Select(step => step with { Reruns = [.. reruns.Value.Where(r => r.Step == step.Name).Select(r => r.Verdict)] })] };
    }
}
