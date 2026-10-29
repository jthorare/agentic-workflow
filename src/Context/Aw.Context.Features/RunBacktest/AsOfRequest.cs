using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Features.RunBacktest;

/// <summary>The as-of map of one sample.</summary>
/// <param name="Repo">The repo: its name and checkout.</param>
/// <param name="Sample">The sample (its cut commit and the issue's creation time decide the map).</param>
/// <param name="Scope">The call's scope: the deny globs and the caller the map is viewed under, and the job it runs for.</param>
/// <param name="Progress">Told when the cluster pass has to wait behind a map build; null to say nothing.</param>
internal sealed record AsOfRequest(ContextRepo Repo, CorpusSample Sample, ContextScope Scope, IProgress<string>? Progress = null);
