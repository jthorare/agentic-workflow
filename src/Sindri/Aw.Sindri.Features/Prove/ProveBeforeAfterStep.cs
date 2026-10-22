using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.prove.before-after' on the block runtime (line spec §7.3, PRV-BA-001): for every live item's <c language="text">base+head</c> signal, its recorded script (the signal's
/// <c language="text">script</c>, a path inside the phase's workspace) runs at the phase's base, in a scratch worktree made for the run, and at the head, in the workspace. It is the
/// workspace's one file that runs in both trees, so the two runs are by the same script. The result is <c language="text">{commit, results[{item, signal, base, head, baseScript, headScript}]}</c>
/// in 'fact:beforeAfter'; 'pass' when every script failed at the base and passed at the head, otherwise 'fail' (a signal with no usable script is recorded as 'missing' with a note, nothing is run, and it does not fail the Step: the spec names no other runnable source).
/// The Step fails (and an unrouted failure parks the instance) when a script cannot run at all, when the scratch worktree cannot be made, or when the context or a phase file is missing.
/// The scratch worktree is removed afterwards (never forced).
/// </summary>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="area">The instance's run area, where the scratch worktree goes.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
/// <param name="worktrees">Makes and removes the scratch worktree.</param>
/// <param name="scripts">Runs a script in a checkout.</param>
/// <param name="commands">The profile's commands, which a script written from the red tests runs.</param>
internal sealed class ProveBeforeAfterStep(IBriefFiles files, IRunArea area, IRepoCheckouts checkouts, IWorktrees worktrees, IScriptRuns scripts, RepoCommandRunner commands) : IBlockStep, IBlockStepId, IRepoStep
{
    private static readonly TimeSpan _scriptTimeout = TimeSpan.FromMinutes(20);

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.before-after");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (EvidenceInputs.Of(context) is not { } inputs || StepFacts.Text(context, "fact:workspace") is not { Length: > 0 } workspace)
        {
            return BlockStepResult.Failed("step.prove.before-after needs the instance's address and repo, fact:phasePacket (with its dir), fact:built (with its head) and fact:workspace");
        }

        var (phase, checkout, failure) = await Load(inputs, cancellationToken).ConfigureAwait(false);
        if (phase is null || checkout is null)
        {
            return BlockStepResult.Failed(failure!);   // why: a null phase comes with its reason
        }

        var planned = await WithRedTestScripts([.. Signals(phase)], (inputs.Address, workspace), StepFacts.Read(context, "fact:redTests"), cancellationToken).ConfigureAwait(false);
        var (rows, unrunnable) = planned.Exists(static p => p.Script is not null)
            ? await RunInScratch(inputs.Address, (phase, checkout), planned, workspace, cancellationToken).ConfigureAwait(false)
            : ([.. planned.Select(static p => (JsonNode)Row(p, "missing", "missing"))], null);
        if (unrunnable is not null)
        {
            return BlockStepResult.Failed(unrunnable);
        }

        var holds = rows.OfType<JsonObject>().All(static r => (string?)r["base"] == "missing" || ((string?)r["base"] == "fail" && (string?)r["head"] == "pass"));   // why: a signal with no runnable script is recorded, not failed (the demo can't block on a field nobody writes)
        return StepFacts.Done(holds ? "pass" : "fail", ("fact:beforeAfter", new JsonObject { ["commit"] = inputs.Head, ["results"] = new JsonArray([.. rows]) }));
    }

    private async ValueTask<(PhaseFiles? Phase, string? Checkout, string? Failure)> Load(EvidenceInputs inputs, CancellationToken cancellationToken)
    {
        var checkout = await checkouts.Of(inputs.Repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return (null, null, $"step.prove.before-after has no checkout of {inputs.Repo.Value}: {checkout.Error.Message}");
        }

        var dir = PhaseDir.Of(inputs.Dir, checkout.Value);
        var (phase, failure) = await PhaseFiles.Load(files, dir, cancellationToken).ConfigureAwait(false);
        return phase is null ? (null, null, $"step.prove.before-after can't load the phase packet {dir}: {failure}") : (phase, checkout.Value, null);
    }

    private async ValueTask<(List<JsonNode> Rows, string? Unrunnable)> RunInScratch(string address, (PhaseFiles Phase, string Checkout) at, List<Planned> planned, string workspace, CancellationToken cancellationToken)
    {
        var (phase, checkout) = at;
        var scratch = area.PathFor(address, "before-after/base");
        if (scratch.IsError)
        {
            return ([], $"step.prove.before-after can't place the scratch worktree: {scratch.Error.Message}");
        }

        var made = await worktrees.Create(new WorktreeRequest(checkout, phase.BaseSha, "sindri/before-after/" + phase.Id, scratch.Value), cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return ([], $"step.prove.before-after can't make the scratch worktree at the base {phase.BaseSha}: {made.Error.Code} {made.Error.Message}");
        }

        try
        {
            return await RunAll(planned, workspace, made.Value.Path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = await worktrees.Remove(made.Value.Path, CancellationToken.None).ConfigureAwait(false);   // why: a tree that can't be removed (SND-WT-004) is left as it is; the result stands
        }
    }

    private async ValueTask<(List<JsonNode> Rows, string? Unrunnable)> RunAll(List<Planned> planned, string workspace, string baseTree, CancellationToken cancellationToken)
    {
        var rows = new List<JsonNode>();
        foreach (var p in planned)
        {
            if (p.Script is null)
            {
                rows.Add(Row(p, "missing", "missing"));
                continue;
            }

            var script = p.Path ?? Path.Combine(workspace, p.Script);
            var atBase = await scripts.Run(baseTree, script, _scriptTimeout, cancellationToken).ConfigureAwait(false);
            var atHead = await scripts.Run(workspace, script, _scriptTimeout, cancellationToken).ConfigureAwait(false);
            if (atBase is null || atHead is null)
            {
                return (rows, $"step.prove.before-after can't run {p.Script} for {p.Item} {p.Signal} at the {(atBase is null ? "base" : "head")}: no shell, no such file or a timeout");
            }

            rows.Add(Row(p, atBase == 0 ? "pass" : "fail", atHead == 0 ? "pass" : "fail"));
        }

        return (rows, null);
    }

    private static JsonObject Row(Planned p, string atBase, string atHead) => new()
    {
        ["item"] = p.Item,
        ["signal"] = p.Signal,
        ["base"] = atBase,
        ["head"] = atHead,
        ["baseScript"] = p.Script,
        ["headScript"] = p.Script,
        ["note"] = p.Script is null ? "the before/after proof was not runnable: the signal records no usable script" : null,
    };

    private static IEnumerable<Planned> Signals(PhaseFiles phase) =>
        ProofChecks.LiveItems(phase.Acceptance).SelectMany(item => (phase.Signals?["signals"]?[item] as JsonArray ?? []).OfType<JsonObject>()
            .Where(static s => (string?)s["runsAt"] == "base+head")
            .Select(s => new Planned(item, (string?)s["id"] ?? string.Empty, Script((string?)s["script"]))));

    // A script is a path inside the workspace: relative, no '..' and no drive.
    private static string? Script(string? path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Split('/', '\\').Contains("..", StringComparer.Ordinal) ? path : null;

    private sealed record Planned(string Item, string Signal, string? Script, string? Path = null);

    // A signal that records no script is proven by its item's red tests (the check that failed before the fix and passes after it): the Step writes one script in the run area that copies the item's red
    // test files into the checkout it runs in (the base tree does not have them yet) and runs the profile's commands there. It is one file, so base and head are by the same script.
    private async ValueTask<List<Planned>> WithRedTestScripts(List<Planned> planned, (string Address, string Workspace) at, JsonNode? redTests, CancellationToken cancellationToken)
    {
        if (!planned.Exists(static p => p.Script is null))
        {
            return planned;
        }

        var listed = await commands.For(cancellationToken).ConfigureAwait(false);
        if (listed.IsError || listed.Value.Commands.Count == 0)
        {
            return planned;
        }

        var set = listed.Value;

        var resolved = new List<Planned>();
        foreach (var p in planned)
        {
            resolved.Add(p.Script is null && TestPaths(redTests, p.Item) is { Count: > 0 } paths
                ? await Written(p, at, RunScript(at.Workspace, paths, set.Commands), cancellationToken).ConfigureAwait(false)
                : p);
        }

        return resolved;
    }

    private async ValueTask<Planned> Written(Planned p, (string Address, string Workspace) at, string content, CancellationToken cancellationToken)
    {
        var name = "before-after/" + string.Concat(p.Item.Select(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_')) + ".sh";
        var path = area.PathFor(at.Address, name);
        if (path.IsError || (await area.Write(at.Address, name, content, append: false, cancellationToken).ConfigureAwait(false)).IsError)
        {
            return p;
        }

        return p with { Script = name, Path = path.Value };
    }

    private static List<string> TestPaths(JsonNode? redTests, string item) =>
        redTests?["items"]?[item]?["tests"] is JsonArray listed
            ? [.. listed.OfType<JsonObject>().Select(static t => Script((string?)t["path"])).OfType<string>().Distinct(StringComparer.Ordinal)]
            : [];

    private static string RunScript(string workspace, List<string> paths, IReadOnlyList<BuildCommand> profile)
    {
        var script = new StringBuilder("#!/usr/bin/env bash\nws=").Append(Quote(workspace)).Append('\n');
        foreach (var path in paths)
        {
            var file = Quote(path);
            _ = script.Append(CultureInfo.InvariantCulture, $"[ {file} -ef \"$ws\"/{file} ] || {{ mkdir -p -- \"$(dirname -- {file})\" && cp -- \"$ws\"/{file} {file}; }}\n");
        }

        _ = script.Append("rc=0\n");
        foreach (var command in profile)
        {
            _ = script.AppendJoin(' ', command.Run.Select(Quote)).Append(" || rc=$?\n");
        }

        return script.Append("exit $rc\n").ToString();
    }

    private static string Quote(string text) => "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
