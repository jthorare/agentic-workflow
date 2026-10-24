using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>A review request read into what the engine takes: the decision, or the review file to apply.</summary>
/// <param name="Verb">The variant.</param>
/// <param name="Decision">The engine decision, or null for apply-file.</param>
/// <param name="Path">The review file for apply-file, or null.</param>
internal sealed record ReviewInput(string Verb, ReviewDecision? Decision, string? Path)
{
    /// <summary>The most overrides one undo may name.</summary>
    internal const int MaxUndoList = 10_000;

    /// <summary>Every variant this command takes.</summary>
    internal static IReadOnlyList<string> Verbs { get; } =
        ["accept", "reject", "rename", "move", "accept-deterministic", "undo", "drop", "repoint", "apply-file", "merge", "add", "delete"];

    /// <summary>The protected variants: they show what they would do and need the human's typed answer (README X63).</summary>
    internal static IReadOnlySet<string> Protected { get; } = new HashSet<string>(["apply-file", "merge", "add", "delete"], StringComparer.Ordinal);

    /// <summary>Whether this variant is protected.</summary>
    internal bool IsProtected => Protected.Contains(Verb);

    /// <summary>Reads <paramref name="request"/>: the verb, and the keys and ids it names; a bad one is SND-CLI-003 naming the field.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The input.</returns>
    internal static Result<ReviewInput> Read(ReviewContextMapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Verbs.Contains(request.Verb, StringComparer.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"'{request.Verb}' is not a review action: {string.Join(" | ", Verbs)}");
        }

        if (request.Verb == "apply-file")
        {
            return string.IsNullOrWhiteSpace(request.Path)
                ? SindriErrors.InvalidInput.With("apply-file names the review file (path)")
                : new ReviewInput(request.Verb, Decision: null, request.Path);
        }

        var parsed = Parse(request);
        return parsed.IsError ? parsed.Error : new ReviewInput(request.Verb, parsed.Value, Path: null);
    }

    private static Result<ReviewDecision> Parse(ReviewContextMapRequest request)
    {
        var node = Field<NodeKey>(request.Node, "node", NodeKey.TryFrom);
        var other = Field<NodeKey>(request.Other, "other", NodeKey.TryFrom);
        var link = Field<LinkId>(request.Link, "link", LinkId.TryFrom);
        var undoes = Field<OverrideId>(request.Override, "override", OverrideId.TryFrom);
        var all = UndoList(request);
        var bad = node.IsError ? node.Error : other.IsError ? other.Error : link.IsError ? link.Error : undoes.IsError ? undoes.Error : all.IsError ? all.Error : null;
        return bad is { } error ? error : new ReviewDecision(ToVerb(request.Verb), node.Value, link.Value, undoes.Value, request.Label, other.Value, all.Value);
    }

    /// <summary>The overrides an undo reverses together: 1 to 10000 ids, only for undo and not beside a single override.</summary>
    private static Result<IReadOnlyList<OverrideId>?> UndoList(ReviewContextMapRequest request)
    {
        if (request.Overrides is not { } ids)
        {
            return Results.Success<IReadOnlyList<OverrideId>?>(value: null);
        }

        var refusal = !string.Equals(request.Verb, "undo", StringComparison.Ordinal) ? "overrides: only undo takes a list of overrides"
            : request.Override is not null ? "overrides: give either 'override' (one id) or 'overrides' (a list), not both"
            : ids.Count is 0 or > MaxUndoList ? $"overrides: undo takes 1 to {MaxUndoList} override ids (fix: sindri context map review --undo <id>[,<id>…])"
            : null;
        if (refusal is not null)
        {
            return SindriErrors.InvalidInput.With(refusal);
        }

        var parsed = new List<OverrideId>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            if (!OverrideId.TryFrom(ids[i], out var id))
            {
                return SindriErrors.InvalidInput.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"overrides[{i}]: '{ids[i]}' is not a valid override id ('ovr_' and 16 hex digits)"));
            }

            parsed.Add(id);
        }

        return Results.Success<IReadOnlyList<OverrideId>?>(parsed);
    }

    private delegate bool TryParse<T>(string text, out T value);

    private static Result<T?> Field<T>(string? text, string name, TryParse<T> parse)
        where T : struct
    {
        if (text is null)
        {
            return (T?)null;
        }

        if (parse(text, out var parsed))
        {
            return (T?)parsed;
        }

        var shape = name switch
        {
            "override" => "override id ('ovr_' and 16 hex digits)",
            "link" => "link id ('edge:' and 16 hex digits)",
            _ => "node key ('feature:tabs', 'surface:command/x', 'code:src/a.ts')",
        };
        return SindriErrors.InvalidInput.With($"{name}: '{text}' is not a valid {shape}");
    }

    private static ReviewVerb ToVerb(string verb) => verb switch
    {
        "accept" => ReviewVerb.Accept,
        "reject" => ReviewVerb.Reject,
        "rename" => ReviewVerb.Rename,
        "move" => ReviewVerb.Move,
        "accept-deterministic" => ReviewVerb.AcceptDeterministic,
        "undo" => ReviewVerb.Undo,
        "drop" => ReviewVerb.Drop,
        "repoint" => ReviewVerb.Repoint,
        "merge" => ReviewVerb.Merge,
        "add" => ReviewVerb.Add,
        _ => ReviewVerb.Delete,
    };
}
