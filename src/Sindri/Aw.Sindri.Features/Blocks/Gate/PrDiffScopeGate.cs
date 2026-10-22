using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.Docs;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.pr.diff-scope' (PB spec §10.1, BD2, BD5) on the paths a push changes (the head the snapshot saw to the workspace's HEAD). 'scope: phase': every path is in the phase packet's
/// allowedFiles (or writeScope; "new: path" names one file) or is a test by convention; an empty packet falls back to the files the PR already changes. 'scope: docs': only 'paths' (the
/// doc globs) and the docs the design names (fact:docsScope). Either way no CI config changes and no test is weakened (BLD-TEST-001: a test deleted, or a skip added to one).
/// </summary>
/// <param name="paths">Lists the paths between two refs.</param>
/// <param name="content">Reads the lines the push added and the paths it deleted.</param>
internal sealed class PrDiffScopeGate(IChangedPaths paths, IDiffContent content) : IGate, IDescribedBlock
{
    private static readonly string[] _skips = ["Skip =", "Skip=", ".skip(", "xit(", "xdescribe(", "xtest(", "@Disabled", "@Ignore", "[Ignore", "pytest.mark.skip", "t.Skip(", "Assert.Skip(", "it.todo("];

    private static readonly string[] _ciRoots = [".github/", ".circleci/", ".buildkite/", ".gitlab/"];

    private static readonly string[] _ciFiles = [".gitlab-ci.yml", "azure-pipelines.yml", "Jenkinsfile", ".travis.yml", "bitbucket-pipelines.yml", "appveyor.yml"];

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = PrGates.DiffScope;

    /// <inheritdoc/>
    public async ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (PrGateInputs.Workspace(context) is not { } workspace || PrGateInputs.SinceHead(context) is not { } since)
        {
            return Refused("gate.pr.diff-scope needs the workspace (a path) and the PR state's head sha, so it can't check the push");
        }

        var changed = await paths.Between(workspace, since, "HEAD", cancellationToken).ConfigureAwait(false);
        if (changed.IsError)
        {
            return Refused($"gate.pr.diff-scope can't list the push's paths in {workspace}: {changed.Error.Message}");
        }

        var allowed = await Allowed(context, workspace, cancellationToken).ConfigureAwait(false);
        if (allowed.IsError)
        {
            return Refused(allowed.Error.Message);
        }

        List<string> reasons = [];
        foreach (var path in changed.Value)
        {
            if (IsCiConfig(path))
            {
                reasons.Add($"{path} is CI configuration; a push never changes it");
            }
            else if (!(allowed.Value.TestsAllowed && IsTest(path)) && !allowed.Value.Matches(path))
            {
                reasons.Add($"{path} is outside the allowed scope");
            }
        }

        var weakened = await Weakened(workspace, since, cancellationToken).ConfigureAwait(false);
        if (weakened.IsError)
        {
            return Refused($"gate.pr.diff-scope can't read the push's diff in {workspace}: {weakened.Error.Message}");
        }

        reasons.AddRange(weakened.Value);
        return reasons.Count == 0 ? new GateResult(GateVerdict.Pass, Score: null, Reasons: []) : new GateResult(GateVerdict.Fail, Score: null, reasons);
    }

    private async ValueTask<Result<Scope>> Allowed(GateContext context, string workspace, CancellationToken cancellationToken)
    {
        var docs = context.Arguments.TryGetValue("scope", out var scope) && scope.ValueKind == JsonValueKind.String && string.Equals(scope.GetString(), "docs", StringComparison.Ordinal);
        if (docs)
        {
            var globs = Strings(context.Arguments.GetValueOrDefault("paths"));
            IEnumerable<string> named = PrGateInputs.Fact(context, "fact:docsScope") is { ValueKind: JsonValueKind.Object } docsScope && docsScope.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array
                ? candidates.EnumerateArray().Where(static c => c.ValueKind == JsonValueKind.Object && c.TryGetProperty("designNamed", out var flag) && flag.ValueKind == JsonValueKind.True)
                    .Select(static c => c.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String ? path.GetString() : null).OfType<string>()
                : [];
            return new Scope([.. globs], [.. named], TestsAllowed: false);
        }

        var phase = context.Arguments.GetValueOrDefault("phase");
        var entries = Strings(Property(phase, "allowedFiles")).Concat(Strings(Property(phase, "writeScope"))).ToList();
        if (entries.Count > 0)
        {
            return new Scope([.. entries.Where(static e => !e.StartsWith("new:", StringComparison.Ordinal))], [.. entries.Where(static e => e.StartsWith("new:", StringComparison.Ordinal)).Select(static e => e["new:".Length..].Trim())], TestsAllowed: true);
        }

        // BD2: an empty phase packet falls back to the PR's changed files (their tests are allowed already).
        if (PrGateInputs.BaseSha(context) is not { } baseSha || PrGateInputs.SinceHead(context) is not { } head)
        {
            return Fail("gate.pr.diff-scope has no phase packet and no PR state to fall back on");
        }

        var own = await paths.Between(workspace, baseSha, head, cancellationToken).ConfigureAwait(false);
        if (own.IsError)
        {
            return Fail($"gate.pr.diff-scope can't list the PR's own files in {workspace}: {own.Error.Message}");
        }

        return new Scope([], [.. own.Value], TestsAllowed: true);
    }

    private async ValueTask<Result<IReadOnlyList<string>>> Weakened(string workspace, string since, CancellationToken cancellationToken)
    {
        var deleted = await content.DeletedPaths(workspace, since, cancellationToken).ConfigureAwait(false);
        if (deleted.IsError)
        {
            return deleted.Error;
        }

        var added = await content.AddedLines(workspace, since, cancellationToken).ConfigureAwait(false);
        if (added.IsError)
        {
            return added.Error;
        }

        List<string> reasons = [.. deleted.Value.Where(IsTest).Select(static p => $"{p} is a deleted test (BLD-TEST-001)")];
        reasons.AddRange(added.Value.Where(line => IsTest(line.Path) && Array.Exists(_skips, skip => line.Text.Contains(skip, StringComparison.Ordinal))).Select(static l => $"{l.Path}:{l.Line} skips a test (BLD-TEST-001)"));
        return Results.Success<IReadOnlyList<string>>(reasons);
    }

    private static bool IsCiConfig(string path) => Array.Exists(_ciRoots, root => path.StartsWith(root, StringComparison.Ordinal)) || Array.Exists(_ciFiles, file => string.Equals(path, file, StringComparison.Ordinal));

    /// <summary>A test by convention: under a test directory, or named like a test file.</summary>
    private static bool IsTest(string path)
    {
        var segments = path.Split('/');
        var name = segments[^1];
        return segments[..^1].Any(static s => s.Equals("test", StringComparison.OrdinalIgnoreCase) || s.Equals("tests", StringComparison.OrdinalIgnoreCase) || s.Equals("__tests__", StringComparison.Ordinal) || s.EndsWith(".Tests", StringComparison.Ordinal))
            || name.Contains(".test.", StringComparison.OrdinalIgnoreCase) || name.Contains(".spec.", StringComparison.OrdinalIgnoreCase) || name.Contains("_test.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("test_", StringComparison.OrdinalIgnoreCase) || Path.GetFileNameWithoutExtension(name).EndsWith("Tests", StringComparison.Ordinal);
    }

    private static JsonElement? Property(JsonElement? node, string name) =>
        node is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out var value) ? value : null;

    private static IEnumerable<string> Strings(JsonElement? node) =>
        node is { ValueKind: JsonValueKind.Array } array ? array.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString()).OfType<string>() : [];

    private static GateResult Refused(string reason) => new(GateVerdict.Fail, Score: null, [reason]);

    private static Error Fail(string message) => WorkflowErrors.EffectFailed.With(message);

    /// <summary>The paths a push may touch: globs, exact files, and (a phase's scope, not the docs') tests by convention.</summary>
    private sealed record Scope(IReadOnlyList<string> Globs, IReadOnlyList<string> Files, bool TestsAllowed)
    {
        internal bool Matches(string path) => Files.Contains(path, StringComparer.Ordinal) || Globs.Any(g => DocGlob.Matches(g, path));
    }
}
