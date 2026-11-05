using System.Globalization;
using Aw.Kernel.Data;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>The gate store's SQL: named constants and the statements that bind them.</summary>
internal static class GateSql
{
    private const char ScopeSeparator = '\n';

    internal static readonly SqlStatement LatestMerge = new(
        "SELECT pr, sha, at FROM merges ORDER BY at DESC, rowid DESC LIMIT 1", Subject: "latest merge");

    internal static SqlStatement NextRun(int pr, int planned) => new(
        "SELECT CASE WHEN $planned > 0 AND NOT EXISTS (SELECT 1 FROM gates WHERE pr = $pr AND run = $planned) THEN $planned ELSE COALESCE((SELECT MAX(run) FROM gates WHERE pr = $pr), 0) + 1 END", c => c.Bind("$pr", pr).Bind("$planned", planned), "next gate run");

    internal static IEnumerable<SqlStatement> Insert(GateRecord gate)
    {
        yield return new SqlStatement(
            "INSERT INTO gates (pr, run, head, main, scopes, verdict, at, tree, stack) VALUES ($pr, $run, $head, $main, $scopes, $verdict, $at, $tree, $stack)",
            c => c.Bind("$pr", gate.Pr).Bind("$run", gate.Run).Bind("$head", gate.Head).Bind("$main", gate.Main)
                .Bind("$scopes", string.Join(ScopeSeparator, gate.Scopes)).Bind("$verdict", gate.Verdict.ToString()).Bind("$at", gate.At)
                .Bind("$tree", gate.Tree).Bind("$stack", string.Join(ScopeSeparator, gate.Stack.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.Pr}:{s.Head}")))),
            "gate");
        for (var position = 0; position < gate.Steps.Count; position++)
        {
            var step = gate.Steps[position];
            var at = position;
            yield return new SqlStatement(
                "INSERT INTO gate_steps (pr, run, position, name, verdict, log) VALUES ($pr, $run, $position, $name, $verdict, $log)",
                c => c.Bind("$pr", gate.Pr).Bind("$run", gate.Run).Bind("$position", at).Bind("$name", step.Name)
                    .Bind("$verdict", step.Verdict.ToString()).Bind("$log", step.Log),
                "gate step " + step.Name);
        }
    }

    internal static SqlStatement Latest(int pr) => new(
        "SELECT pr, run, head, main, scopes, verdict, at, tree, stack FROM gates WHERE pr = $pr ORDER BY run DESC LIMIT 1",
        c => c.Bind("$pr", pr),
        "latest gate");

    internal static SqlStatement All(int pr) => new(
        "SELECT pr, run, head, main, scopes, verdict, at, tree, stack FROM gates WHERE pr = $pr ORDER BY run DESC",
        c => c.Bind("$pr", pr),
        "gate history");

    internal static SqlStatement Steps(int pr, int run) => new(
        "SELECT name, verdict, log FROM gate_steps WHERE pr = $pr AND run = $run ORDER BY position ASC",
        c => c.Bind("$pr", pr).Bind("$run", run),
        "gate steps");

    internal static SqlStatement HasFailedStep(int pr, int run, string step) => new(
        "SELECT COUNT(*) FROM gate_steps WHERE pr = $pr AND run = $run AND name = $name AND verdict <> 'Pass'",
        c => c.Bind("$pr", pr).Bind("$run", run).Bind("$name", step),
        "gate step " + step);

    internal static SqlStatement Classify(int pr, int run, string step, TestVerdict verdict) => new(
        "INSERT OR REPLACE INTO gate_reruns (pr, run, step, test, verdict) VALUES ($pr, $run, $step, $test, $verdict)",
        c => c.Bind("$pr", pr).Bind("$run", run).Bind("$step", step).Bind("$test", verdict.Test).Bind("$verdict", verdict.Verdict.ToString()),
        "gate rerun " + step);

    internal static SqlStatement Reruns(int pr, int run) => new(
        "SELECT step, test, verdict FROM gate_reruns WHERE pr = $pr AND run = $run ORDER BY rowid ASC",
        c => c.Bind("$pr", pr).Bind("$run", run),
        "gate reruns");

    internal static SqlStatement MergeOf(int pr) => new(
        "SELECT pr, sha, at FROM merges WHERE pr = $pr ORDER BY at DESC, rowid DESC LIMIT 1",
        c => c.Bind("$pr", pr),
        "merge of a pull request");

    internal static SqlStatement InsertMerge(MergeRecord merge) => new(
        "INSERT OR REPLACE INTO merges (pr, sha, at) VALUES ($pr, $sha, $at)",
        c => c.Bind("$pr", merge.Pr).Bind("$sha", merge.Sha).Bind("$at", merge.At),
        "merge");

    internal static GateRecord ReadHead(SqliteDataReader r) => new(
        checked((int)r.GetInt64(0)),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4).Split(ScopeSeparator, StringSplitOptions.RemoveEmptyEntries),
        [],
        Enum.Parse<GateVerdict>(r.GetString(5)),
        SqliteRead.GetTimestamp(r, 6))
    {
        Run = checked((int)r.GetInt64(1)),
        Tree = r.IsDBNull(7) ? null : r.GetString(7),
        Stack = [.. r.GetString(8).Split(ScopeSeparator, StringSplitOptions.RemoveEmptyEntries).Select(ReadStacked)],
    };

    private static StackedPr ReadStacked(string entry)
    {
        var colon = entry.IndexOf(':', StringComparison.Ordinal);
        return new StackedPr(int.Parse(entry.AsSpan(0, colon), CultureInfo.InvariantCulture), entry[(colon + 1)..]);
    }

    internal static GateStep ReadStep(SqliteDataReader r) =>
        new(r.GetString(0), Enum.Parse<GateVerdict>(r.GetString(1)), r.GetString(2));

    internal static (string Step, TestVerdict Verdict) ReadRerun(SqliteDataReader r) =>
        (r.GetString(0), new TestVerdict(r.GetString(1), Enum.Parse<FlakeVerdict>(r.GetString(2))));

    internal static MergeRecord ReadMerge(SqliteDataReader r) => new(checked((int)r.GetInt64(0)), r.GetString(1), SqliteRead.GetTimestamp(r, 2));
}
