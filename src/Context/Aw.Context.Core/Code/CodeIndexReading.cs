using Aw.Context.Core.Lenses;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Core.Code;

/// <summary>Pure rules for reading the code index (code index spec §7.3): the as-of comparison, a head's rows and the lens states the block's states map onto.</summary>
public static class CodeIndexReading
{
    private const int MinCommitLength = 7;
    private const string CatchingUpReason = "catching-up";

    /// <summary>Whether a text is a commit sha (7 to 64 lowercase hex characters), the only kind of 'as of' a source is asked for.</summary>
    /// <param name="commit">The text.</param>
    /// <returns>True for a sha.</returns>
    public static bool IsCommit(string? commit) => commit is { Length: >= 7 and <= 64 } && commit.All(char.IsAsciiHexDigitLower);

    /// <summary>Whether two commits name the same commit; either may be abbreviated (seven characters or more).</summary>
    /// <param name="left">A commit.</param>
    /// <param name="right">Another commit.</param>
    /// <returns>True when one is a prefix of the other.</returns>
    public static bool Same(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Length >= MinCommitLength && right.Length >= MinCommitLength
            && (left.StartsWith(right, StringComparison.OrdinalIgnoreCase) || right.StartsWith(left, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The head of an index read against the map's commit: a readable index that is not at that commit is <see cref="CodeIndexState.Stale"/> (both commits kept), computed at read time.</summary>
    /// <param name="own">What the block says by itself (head moved, checkout off main, layer down, ...), with no as-of commit.</param>
    /// <param name="asOf">The map version's commit, or null.</param>
    /// <returns>The head.</returns>
    public static CodeIndexHead Head(CodeIndexHead own, string? asOf)
    {
        ArgumentNullException.ThrowIfNull(own);
        var behind = !Unreadable(own.State) && own.Commit is not null && asOf is not null && !Same(own.Commit, asOf);
        return behind ? own with { State = own.State is CodeIndexState.Ok or CodeIndexState.LayerUnavailable ? CodeIndexState.Stale : own.State, AsOfCommit = asOf } : own with { AsOfCommit = null };
    }

    /// <summary>The head of a stale index whose commit was found to be the merge base of the index and the map (an ancestor): catching-up, with the files changed between them as pending. Any other stale head stays as it is.</summary>
    /// <param name="stale">The head <see cref="Head"/> gave.</param>
    /// <param name="mergeBase">The merge base of the index's commit and the map's.</param>
    /// <param name="pending">The files changed from the index's commit to the map's.</param>
    /// <returns>The head.</returns>
    public static CodeIndexHead CatchingUp(CodeIndexHead stale, string mergeBase, IReadOnlyList<string> pending)
    {
        ArgumentNullException.ThrowIfNull(stale);
        ArgumentNullException.ThrowIfNull(mergeBase);
        ArgumentNullException.ThrowIfNull(pending);
        // A layer that is down is a fault whatever the history; an index that is not the merge base is not an ancestor of the map's commit.
        var trails = stale is { State: CodeIndexState.Stale, Layer: null, Commit: { } commit } && Same(commit, mergeBase);
        return trails ? stale with { State = CodeIndexState.CatchingUp, Pending = [.. pending.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] } : stale;
    }

    /// <summary>What a row of an answer carries for one file: like <see cref="Stamp(CodeIndexHead)"/>, except that a catching-up index is 'stale' only for the files pending and 'ok' for the others.</summary>
    /// <param name="head">The head the rows were read under.</param>
    /// <param name="path">The repo-relative path of the row's file.</param>
    /// <returns>The row state and the as-of commit to show, or null.</returns>
    public static (SymbolsState State, string? AsOfCommit) Stamp(CodeIndexHead head, string path)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(path);
        if (head.State != CodeIndexState.CatchingUp)
        {
            return Stamp(head);
        }

        // A file the index trails on may differ at the map's commit; any other file is the same there, so its rows are current.
        return head.Pending?.Contains(path, StringComparer.Ordinal) == true ? (SymbolsState.Stale, head.AsOfCommit) : (SymbolsState.Ok, null);
    }

    /// <summary>Whether the head names an index that can be read: a commit and a state that is not missing, damaged, still building or not configured.</summary>
    /// <param name="head">The head.</param>
    /// <returns>True when rows can be asked for.</returns>
    public static bool Readable(CodeIndexHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        return head.Commit is not null && !Unreadable(head.State);
    }

    /// <summary>Whether the index and the map are at different commits.</summary>
    /// <param name="head">The head.</param>
    /// <returns>True when both commits are known and differ.</returns>
    public static bool AsOfMismatch(CodeIndexHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        return head.Commit is not null && head.AsOfCommit is not null && !Same(head.Commit, head.AsOfCommit);
    }

    /// <summary>What every row of an answer carries: 'stale' (and the map's commit when it differs) or 'ok'. A catching-up index is stale for any row whose file is not known; see <see cref="Stamp(CodeIndexHead, string)"/>.</summary>
    /// <param name="head">The head the rows were read under.</param>
    /// <returns>The row state and the as-of commit to show, or null.</returns>
    public static (SymbolsState State, string? AsOfCommit) Stamp(CodeIndexHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (Unreadable(head.State))
        {
            return (SymbolsState.Unavailable, null);
        }

        return (head.State is CodeIndexState.Stale or CodeIndexState.CheckoutNotOnMain or CodeIndexState.CatchingUp ? SymbolsState.Stale : SymbolsState.Ok, AsOfMismatch(head) ? head.AsOfCommit : null);
    }

    /// <summary>The 'code-index' row of 'searched.sources' for a head.</summary>
    /// <param name="head">The head.</param>
    /// <returns>ok, degraded (stale, checkout-not-on-main, or the layer), unavailable (index-not-built, index-damaged, index-building) or not-configured.</returns>
    public static SearchedSource Row(CodeIndexHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        return head.State switch
        {
            CodeIndexState.Ok => Make(SearchedState.Ok, reason: null, code: null, head.BuiltAt),
            CodeIndexState.CatchingUp => Make(SearchedState.Ok, CatchingUpReason, code: null, head.BuiltAt) with { CatchingUp = new SearchedCatchingUp(head.Commit ?? string.Empty, head.AsOfCommit ?? string.Empty, head.Pending ?? []) },
            CodeIndexState.Stale => Make(SearchedState.Degraded, "stale", head.Code, head.BuiltAt),
            CodeIndexState.CheckoutNotOnMain => Make(SearchedState.Degraded, "checkout-not-on-main", head.Code, head.BuiltAt),
            CodeIndexState.LayerUnavailable => Make(SearchedState.Degraded, head.Layer ?? "layer", head.Code, head.BuiltAt),
            CodeIndexState.Damaged => Make(SearchedState.Unavailable, "index-damaged", head.Code, asOf: null),
            CodeIndexState.Building => Make(SearchedState.Unavailable, "index-building", head.Code, asOf: null),
            CodeIndexState.NotConfigured => Make(SearchedState.NotConfigured, "not-configured", head.Code, asOf: null),
            CodeIndexState.Missing => Make(SearchedState.Unavailable, CodeSymbols.IndexNotBuilt, head.Code, asOf: null),
            _ => Make(SearchedState.Unavailable, CodeSymbols.IndexNotBuilt, head.Code, asOf: null),
        };
    }

    /// <summary>The row for a read that failed: degraded, with the error's code.</summary>
    /// <param name="code">The error's code.</param>
    /// <returns>The row.</returns>
    public static SearchedSource FailedRow(string code) => Make(SearchedState.Degraded, reason: null, code, asOf: null);

    /// <summary>The callers or callees of the code lens for a head and the edges read under it.</summary>
    /// <param name="head">The head.</param>
    /// <param name="rows">The edges (none when the index is unreadable).</param>
    /// <param name="unresolved">The call sites that resolved to nothing.</param>
    /// <returns>The state, reason and rows, with both commits when the index is not at the map's.</returns>
    public static CodeSymbols Calls(CodeIndexHead head, IReadOnlyList<CallEdgeRow> rows, int unresolved)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(rows);
        if (Unreadable(head.State))
        {
            var none = head.State == CodeIndexState.Missing ? CodeSymbols.Unavailable : CodeSymbols.Unavailable with { Reason = UnreadableReason(head.State, hyphen: true) };
            return head.Code is null ? none : none with { Code = head.Code };
        }

        var state = head.State == CodeIndexState.CatchingUp ? (rows.Any(static r => r.State == SymbolsState.Stale) ? SymbolsState.Stale : SymbolsState.Ok) : Stamp(head).State;
        return new CodeSymbols(state, Reason(head, calls: true), rows)
        {
            Commit = head.Commit,
            AsOfCommit = AsOfMismatch(head) ? head.AsOfCommit : null,
            AsOfMismatch = AsOfMismatch(head),
            Unresolved = unresolved,
        };
    }

    /// <summary>The file map's 'symbols' object for a head and the files read under it.</summary>
    /// <param name="head">The head.</param>
    /// <param name="files">The rows of the files listed.</param>
    /// <returns>The state, reason and per-file symbols, with both commits when the index is not at the map's.</returns>
    public static LensSymbols FileMap(CodeIndexHead head, IReadOnlyList<FileSymbolsRow> files)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(files);
        if (Unreadable(head.State))
        {
            var none = head.State == CodeIndexState.Missing ? LensSymbols.Unavailable : LensSymbols.Unavailable with { Reason = UnreadableReason(head.State, hyphen: false) };
            return head.Code is null ? none : none with { Code = head.Code };
        }

        var state = head.State == CodeIndexState.CatchingUp ? (files.Any(static f => f.State == SymbolsState.Stale) ? SymbolsState.Stale : SymbolsState.Ok) : Stamp(head).State;
        return new LensSymbols(state, Reason(head, calls: false), [])
        {
            Files = files,
            Commit = head.Commit,
            AsOfCommit = AsOfMismatch(head) ? head.AsOfCommit : null,
            AsOfMismatch = AsOfMismatch(head),
        };
    }

    /// <summary>The head that limits an answer over several repos: the least healthy one.</summary>
    /// <param name="heads">The heads, at least one.</param>
    /// <returns>The one to report.</returns>
    public static CodeIndexHead Worst(IReadOnlyList<CodeIndexHead> heads)
    {
        ArgumentNullException.ThrowIfNull(heads);
        return heads.MaxBy(h => Weight(h.State)) ?? throw new ArgumentException("at least one head is needed", nameof(heads));
    }

    private static bool Unreadable(CodeIndexState state) => state is CodeIndexState.Missing or CodeIndexState.Damaged or CodeIndexState.Building or CodeIndexState.NotConfigured;

    private static int Weight(CodeIndexState state) => state switch
    {
        CodeIndexState.Ok => 0,
        CodeIndexState.CatchingUp => 1,
        CodeIndexState.LayerUnavailable => 2,
        CodeIndexState.Stale => 3,
        CodeIndexState.CheckoutNotOnMain => 4,
        CodeIndexState.NotConfigured => 5,
        CodeIndexState.Building => 6,
        CodeIndexState.Missing => 7,
        CodeIndexState.Damaged => 8,
        _ => 8,
    };

    private static string UnreadableReason(CodeIndexState state, bool hyphen) => (state, hyphen) switch
    {
        (CodeIndexState.Damaged, true) => "index-damaged",
        (CodeIndexState.Damaged, false) => "index damaged",
        (CodeIndexState.Building, true) => "index-building",
        (CodeIndexState.Building, false) => "index building",
        (CodeIndexState.NotConfigured, true) => "not-configured",
        (CodeIndexState.NotConfigured, false) => "not configured",
        (_, true) => CodeSymbols.IndexNotBuilt,
        _ => "index not built",
    };

    // 'stale' for an index that is behind the map or the head; the code lens also notes a layer that is down, the file map does not (a file's symbols come from the structure layer, which is always on).
    private static string? Reason(CodeIndexHead head, bool calls) => head.State switch
    {
        CodeIndexState.Stale => "stale",
        CodeIndexState.CatchingUp => CatchingUpReason,
        CodeIndexState.CheckoutNotOnMain => "checkout-not-on-main",
        CodeIndexState.LayerUnavailable when calls => $"{head.Layer ?? "layer"}-unavailable",
        CodeIndexState.Ok or CodeIndexState.LayerUnavailable or CodeIndexState.Missing or CodeIndexState.Damaged or CodeIndexState.Building or CodeIndexState.NotConfigured => null,
        _ => null,
    };

    private static SearchedSource Make(SearchedState state, string? reason, string? code, DateTimeOffset? asOf) => new(SourceId.From("code-index"), state, reason, code, asOf);
}
