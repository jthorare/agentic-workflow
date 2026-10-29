using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Tests.Eval.Backtest;

/// <summary>Small histories and issues for the corpus and scoring tests.</summary>
internal static class Builders
{
    public static readonly ContextRepoName Repo = ContextRepoName.From("aw-split-panes");

    public static DateTimeOffset Day(int d) => new(2026, 7, d, 9, 0, 0, TimeSpan.Zero);

    public static HistoryCommit Commit(string sha, int day) => new(sha, Day(day), "work " + sha, Branch: null, IsMerge: false, []);

    public static HistoryCommit Merge(string sha, int day, string subject, params string[] files) => new(sha, Day(day), subject, Branch: null, IsMerge: true, files);

    public static HistoryCommit MergeFrom(string sha, int day, string subject, string branch, params string[] files) => new(sha, Day(day), subject, branch, IsMerge: true, files);

    public static BriefIssue Issue(string key, int createdDay, int updatedDay) => new(key, "linear/" + key, "v1", Day(createdDay), Day(updatedDay));

    public static RepoHistory History(params HistoryCommit[] commits) => new(Repo, commits);

    public static CorpusSample Sample(string key = "SPL-101", params string[] truthFiles) =>
        new(key, "linear/" + key, "v1", Day(2), Leaky: true, Repo, "m1", "c1", truthFiles.Length == 0 ? ["src/a.ts"] : truthFiles);
}
