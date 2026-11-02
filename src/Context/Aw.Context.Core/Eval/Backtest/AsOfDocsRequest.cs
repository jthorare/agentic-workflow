using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Which repo docs to open as they were at a commit.</summary>
/// <param name="Repo">The repo: its name and checkout.</param>
/// <param name="Commit">The cut commit.</param>
/// <param name="Globs">The doc globs ('context.docs.globs' plus a '**/name' glob for each 'context.docs.directoryNames' entry).</param>
/// <param name="Scrubbers">This call's scrubbers (the docs are cleaned with them before anything searches them).</param>
public sealed record AsOfDocsRequest(ContextRepo Repo, string Commit, IReadOnlyList<string> Globs, Scrubbers Scrubbers);
