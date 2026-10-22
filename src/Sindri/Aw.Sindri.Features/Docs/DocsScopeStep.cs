using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Docs;

/// <summary>
/// 'step.docs.scope' on the block runtime (docs-sync spec §7.2, W28): lists the docs that mention a changed path by a plain text search, and the docs the phase packet's
/// 'design.md' names (marked 'designNamed'). The changed paths come from git base...head in the repo's checkout, so no forge read is needed; the pull request's
/// state, base and head are read from 'fact:pr' ('{ state?, base: { ref, sha? }, head: { ref, sha } }', 'fact:prState' fills what it lacks). With docs listed it cuts
/// its own worktree 'sindri/docs/&lt;instance&gt;' at the head (W28) and reports 'candidates'; with none it removes the worktree again and reports 'none'.
/// 'fact:docsScope' is { base (the head the worktree starts at), paths (the globs), candidates: [{ path, designNamed }] }. Symbols, commands and config keys are not
/// searched yet: only the changed paths and their file names.
/// </summary>
/// <param name="checkouts">Where the repo is checked out.</param>
/// <param name="worktrees">Makes the docs worktree.</param>
/// <param name="changed">The paths the pull request changes.</param>
/// <param name="tracked">The files the docs worktree holds.</param>
/// <param name="files">Reads the docs and the design.</param>
internal sealed class DocsScopeStep(IRepoCheckouts checkouts, IWorktrees worktrees, IChangedPaths changed, ITrackedFiles tracked, IBriefFiles files) : IBlockStep, IBlockStepId, IRepoStep
{
    private const int MinNameLength = 6;   // a shorter file name ('a.cs') would match half the docs

    private static readonly string[] _defaultGlobs = ["docs/**", "*.md"];

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.docs.scope");

    private sealed record Pull(string? State, string? BaseRef, string? Head);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enabled(context))
        {
            return Result("disabled");
        }

        var pull = ReadPull(context);
        if (pull.State is { } state && !string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
        {
            return Result("notOpen");
        }

        if (!IsSha(pull.Head) || pull.BaseRef is null || context.Repo is not { } repo)
        {
            return BlockStepResult.Failed("step.docs.scope needs fact:pr with a head commit sha and a base, and the instance's repo");
        }

        var checkout = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return BlockStepResult.Failed($"step.docs.scope has no checkout of {repo.Value}: {checkout.Error.Message}");
        }

        return await Scope(context, checkout.Value, pull.BaseRef, pull.Head!, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Scope(StepContext context, string checkout, string baseRef, string head, CancellationToken cancellationToken)
    {
        var paths = await changed.Between(checkout, baseRef, head, cancellationToken).ConfigureAwait(false);
        if (paths.IsError)
        {
            return BlockStepResult.Failed($"step.docs.scope can't compare {baseRef} with {head}: {paths.Error.Message}");
        }

        var globs = Globs(context);
        var tree = Path.Combine(checkout, ".claude", "worktrees", "sindri-docs-" + Segment(context.Address));
        var made = await worktrees.Create(new WorktreeRequest(checkout, head, "sindri/docs/" + Segment(context.Address), tree), cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return BlockStepResult.Failed($"step.docs.scope can't place the docs worktree: {made.Error.Code} {made.Error.Message}");
        }

        var candidates = await Candidates(context, made.Value.Path, globs, paths.Value, cancellationToken).ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            _ = await worktrees.Remove(made.Value.Path, cancellationToken).ConfigureAwait(false);   // nothing to edit: the worktree is not left behind
            return Result("none", head, 0);
        }

        return Listed(context, made.Value.Path, head, globs, candidates);
    }

    private async ValueTask<List<(string Path, bool DesignNamed)>> Candidates(StepContext context, string tree, List<string> globs, IReadOnlyList<string> changedPaths, CancellationToken cancellationToken)
    {
        var listed = await tracked.Paths(tree, cancellationToken).ConfigureAwait(false);
        var docs = listed.IsError ? [] : listed.Value.Where(path => globs.Exists(g => DocGlob.Matches(g, path))).ToList();
        var needles = Needles(changedPaths.Where(path => !globs.Exists(g => DocGlob.Matches(g, path))));
        var design = await DesignText(context, cancellationToken).ConfigureAwait(false);
        List<(string, bool)> candidates = [];
        foreach (var doc in docs)
        {
            var named = design is not null && design.Contains(doc, StringComparison.Ordinal);
            if (named || await Mentions(tree, doc, needles, cancellationToken).ConfigureAwait(false))
            {
                candidates.Add((doc, named));
            }
        }

        return candidates;
    }

    private async ValueTask<bool> Mentions(string tree, string doc, List<string> needles, CancellationToken cancellationToken)
    {
        if (needles.Count == 0)
        {
            return false;
        }

        var read = await files.Read(doc, [tree], cancellationToken).ConfigureAwait(false);
        return read.IsSuccess && needles.Exists(needle => read.Value.Text.Contains(needle, StringComparison.Ordinal));
    }

    // The phase packet's design.md, when the instance holds a packet with a directory; null otherwise.
    private async ValueTask<string?> DesignText(StepContext context, CancellationToken cancellationToken)
    {
        if (StepFacts.Read(context, "fact:phasePacket") is not JsonObject packet || (string?)packet["dir"] is not { Length: > 0 } dir)
        {
            return null;
        }

        var read = await files.Read("design.md", [dir], cancellationToken).ConfigureAwait(false);
        return read.IsSuccess ? read.Value.Text : null;
    }

    private static List<string> Needles(IEnumerable<string> paths) => [.. paths
        .SelectMany(static path => new[] { path, path[(path.LastIndexOf('/') + 1)..] })
        .Where(static needle => needle.Length >= MinNameLength || needle.Contains('/', StringComparison.Ordinal))
        .Distinct(StringComparer.Ordinal)];

    private static BlockStepResult Listed(StepContext context, string tree, string head, List<string> globs, List<(string Path, bool DesignNamed)> candidates)
    {
        var scope = new JsonObject
        {
            ["base"] = head,
            ["paths"] = new JsonArray([.. globs.Select(static g => (JsonNode)JsonValue.Create(g)!)]),
            ["candidates"] = new JsonArray([.. candidates.Select(static c => (JsonNode)new JsonObject { ["path"] = c.Path, ["designNamed"] = c.DesignNamed })]),
        };
        return StepFacts.Done(
            "candidates",
            ("fact:docsResult", Summary("candidates", head, candidates.Count, context)),
            ("fact:docsScope", scope),
            ("fact:prState", StepFacts.Read(context, "fact:prState")?.DeepClone() ?? StepFacts.Read(context, "fact:pr")?.DeepClone() ?? new JsonObject()),
            ("fact:findings", new JsonArray()),
            ("fact:handoffNote", JsonValue.Create(string.Empty)),
            ("fact:workspace", JsonValue.Create(tree)));
    }

    private static BlockStepResult Result(string outcome, string? head = null, int scanned = 0) => StepFacts.Done(outcome, ("fact:docsResult", new JsonObject { ["result"] = outcome, ["headSha"] = head, ["scanned"] = scanned, ["candidates"] = 0, ["phasePacket"] = null }));

    private static JsonObject Summary(string outcome, string head, int candidates, StepContext context) => new()
    {
        ["result"] = outcome,
        ["headSha"] = head,
        ["scanned"] = candidates,
        ["candidates"] = candidates,
        ["phasePacket"] = StepFacts.Read(context, "fact:phasePacket") is JsonObject packet ? packet["hash"]?.DeepClone() : null,
    };

    private static Pull ReadPull(StepContext context)
    {
        var pr = StepFacts.Read(context, "fact:pr") as JsonObject;
        var state = StepFacts.Read(context, "fact:prState") as JsonObject;
        return new Pull(
            Text(pr?["state"]) ?? Text(state?["state"]),
            Text(pr?["base"]?["sha"]) ?? Text(pr?["base"]?["ref"]) ?? Text(state?["base"]?["sha"]) ?? Text(state?["base"]?["ref"]),
            Text(pr?["head"]?["sha"]) ?? Text(pr?["headSha"]) ?? Text(state?["head"]?["sha"]));
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    // The profile's 'workflow.docs.sync' is 'on' or 'off'; a recipe may also pass a bool. Absent means on.
    private static bool Enabled(StepContext context) => !context.Arguments.TryGetValue("enabled", out var value) || value.ValueKind switch
    {
        JsonValueKind.False => false,
        JsonValueKind.String => !string.Equals(value.GetString(), "off", StringComparison.OrdinalIgnoreCase) && !string.Equals(value.GetString(), "false", StringComparison.OrdinalIgnoreCase),
        _ => true,
    };

    private static List<string> Globs(StepContext context)
    {
        if (!context.Arguments.TryGetValue("paths", out var value))
        {
            return [.. _defaultGlobs];
        }

        List<string> globs = value.ValueKind switch
        {
            JsonValueKind.Array => [.. value.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString()!)],
            JsonValueKind.String => [value.GetString()!],
            _ => [],
        };
        return globs.Count == 0 ? [.. _defaultGlobs] : globs;
    }

    // The instance address ('T-1/docs-sync/1') as one plain branch and directory segment.
    private static string Segment(string? address) => string.Concat((address ?? "run").Select(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-')).TrimStart('.');

    private static bool IsSha(string? value) => value is { Length: 40 } && value.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
