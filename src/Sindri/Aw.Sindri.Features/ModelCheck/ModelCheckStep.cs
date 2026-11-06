using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>
/// 'step.model.check' (model-check design §3): runs the installed TLC runner (<c language="text">tlc-check.sh</c>) over the workspace's spec folders under one heavy slot and maps each result line to an outcome:
/// pass, skipped, violation, vacuous, specError, tooBig or cantRun. The result word of each line decides, never the exit code (only exit 3, "java or the jar is missing", is read). A model is checked
/// only when TLC printed PASS for it in this run.
/// </summary>
/// <param name="folders">Lists the spec folders.</param>
/// <param name="changes">Reads the paths a change touched, for 'scope: touched'.</param>
/// <param name="commands">Runs the runner under one heavy slot.</param>
/// <param name="scrubber">Takes secrets out of a trace.</param>
/// <param name="setup">The runner's path and the environment it gets.</param>
/// <param name="packets">Reads the packet in force, for the scoping model's move into specs/tla/ (MDL-SPEC-003).</param>
/// <param name="scratch">Makes the run's own TMPDIR.</param>
internal sealed partial class ModelCheckStep(ISpecFolders folders, IBranchChanges changes, RepoCommandRunner commands, ITextScrubber scrubber, ModelCheckSetup setup, IPacketDirectory packets, IScratchDirectories scratch) : IBlockStep, IBlockStepId
{
    private readonly ModelPaths _paths = new(folders, packets);

    /// <summary>The most characters of the runner's output the step keeps: TLC prints a full trace under every check that is not a PASS.</summary>
    internal const int OutputLimit = 256 * 1024;

    private const int CheckSeconds = 180;
    private const string ViolationRule = "MDL-VIOLATION-001";
    private const string WitnessRule = "MDL-WITNESS-001";
    private const string SpecErrorRule = "MDL-SPEC-ERROR-001";
    private const string TooBigRule = "MDL-TOOBIG-001";
    private const string ToolRule = "MDL-TOOL-001";
    private const string WarningRule = "MDL-SPEC-002";

    private static readonly Regex _wrong = new(@"^\S+\s+(?<actual>\S+)\s+\(expected (?<expected>[^)]+)\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex _constant = new(@"^\s*(?:CONSTANTS?\s+)?(?<name>\w+)\s*=\s*(?<value>\S.*?)\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.model.check");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Text(context, "fact:workspace") is not { } workspace)
        {
            return BlockStepResult.Failed("step.model.check needs fact:workspace (a path)");
        }

        var options = ModelCheckOptions.From(context);
        var listed = await folders.List(workspace, options.Specs, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return BlockStepResult.Failed($"step.model.check can't list {options.Specs} in {workspace}: {listed.Error.Message}");
        }

        var all = listed.Value.Select(static f => new Spec(f, ModelReadme.Parse(ReadmeText(f)))).ToList();
        if (!options.Specs.StartsWith("model/", StringComparison.Ordinal))
        {
            // A scoping model's paths can't exist before the code does; for the build, the move into specs/tla/ is checked before TLC runs.
            var moved = await _paths.Move(context, workspace, all, cancellationToken).ConfigureAwait(false);
            if (moved.IsError || moved.Value.Count > 0)
            {
                return moved.IsError ? BlockStepResult.Failed($"step.model.check can't read the packet in force: {moved.Error.Message}") : Findings("specError", moved.Value);
            }
        }

        var chosen = all;
        if (options.Touched)
        {
            var touched = await TouchedBy(context, all, cancellationToken).ConfigureAwait(false);
            if (touched.IsError)
            {
                return BlockStepResult.Failed($"step.model.check: {touched.Error.Message}");
            }

            chosen = touched.Value;
        }

        return await CheckChosen(new Request(workspace, options, all, chosen), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> CheckChosen(Request request, CancellationToken cancellationToken)
    {
        var rules = request.Chosen.SelectMany(static s => SpecRules.Check(s.Folder)).ToList();
        var runnable = request.Chosen.Where(s => !rules.Exists(r => r.Path is { } p && p.StartsWith(s.Folder.Path + "/", StringComparison.Ordinal))).ToList();
        if (runnable.Count == 0)
        {
            return rules.Count > 0 ? Findings("specError", rules) : Skipped(request.All);
        }

        if (setup.Runner is not { } runner)
        {
            return Findings("cantRun", [Tool("the TLC runner isn't installed next to sindri (fix: reinstall with scripts/install-cs.sh)")]);
        }

        var stale = request.Options.Specs.StartsWith("model/", StringComparison.Ordinal) ? [] : await _paths.Stale(request.Workspace, runnable, cancellationToken).ConfigureAwait(false);
        var ran = await RunRunner(runner, request.Workspace, runnable, request.Options, cancellationToken).ConfigureAwait(false);
        return ran.IsError
            ? BlockStepResult.Failed($"step.model.check: {ran.Error.Message}")
            : Judge(ran.Value.Single().Run, new Judged(runnable, rules, request.Options, stale));
    }

    private static string ReadmeText(SpecFolder folder) => folder.Files.FirstOrDefault(static f => f.Kind == SpecFileKind.Regular && string.Equals(f.Name, "README.md", StringComparison.Ordinal))?.Text ?? string.Empty;

    private static string NameOf(SpecFolder folder) => folder.Path[(folder.Path.LastIndexOf('/') + 1)..];

    // The folders whose modelled code or own files the change touched; a folder with no 'models:' line is always kept.
    private async ValueTask<Result<List<Spec>>> TouchedBy(StepContext context, List<Spec> all, CancellationToken cancellationToken)
    {
        var change = StepFacts.Read(context, "fact:change");
        if (StepFacts.Field(change, "repo") is not { } repo || StepFacts.Field(change, "base") is not { } baseSha || StepFacts.Head(change) is not { } head)
        {
            return Results.Failure<List<Spec>>(Core.SindriErrors.InvalidInput.With("scope: touched needs fact:change ({ repo, base, head })"));
        }

        var seen = await changes.Between(repo, baseSha, head, cancellationToken).ConfigureAwait(false);
        return seen.IsError
            ? seen.Error
            : all.Where(s => s.Readme.Models is null || seen.Value.Files.Any(f => s.Readme.Touches(s.Folder.Path, f.Path))).ToList();
    }

    private static BlockStepResult Skipped(List<Spec> all) => StepFacts.Done(
        "skipped",
        ("fact:modelCheck", new JsonObject { ["skipped"] = true, ["looked"] = Looked(all), ["specs"] = new JsonArray(), ["specFolders"] = all.Count }));

    private static JsonArray Looked(List<Spec> all) => new([.. all.SelectMany(static s => s.Readme.Models ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(static p => (JsonNode?)p)]);

    // One TMPDIR per run under the setup's base: made before the runner starts and removed in a finally, so a timeout or a cancel cleans up too, and two steps at once never share one.
    private async ValueTask<Result<IReadOnlyList<CheckResult>>> RunRunner(string runner, string workspace, List<Spec> runnable, ModelCheckOptions options, CancellationToken cancellationToken)
    {
        var made = scratch.Make(setup.Tmp);
        if (made.IsError)
        {
            return made.Error;
        }

        try
        {
            return await RunRunnerIn(made.Value, new Invocation(runner, workspace, runnable, options), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scratch.Remove(made.Value);
        }
    }

    private async ValueTask<Result<IReadOnlyList<CheckResult>>> RunRunnerIn(string tmp, Invocation call, CancellationToken cancellationToken)
    {
        var (runner, workspace, runnable, options) = call;
        List<string> argv =
        [
            "env", "-i",
            $"PATH={setup.Path}", $"HOME={setup.Home}", $"AW_STATE_DIR={setup.StateDir}", $"TLC_SH={setup.TlcSh}", $"TLC_HEAP={setup.TlcHeap}", $"TMPDIR={tmp}",
            runner, "--root", workspace, "--timeout", CheckSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ];
        if (options.Witnesses)
        {
            argv.Add("--witnesses");
        }

        argv.AddRange(runnable.Select(s => $"{workspace.TrimEnd('/')}/{s.Folder.Path}"));
        var budget = RepoCommandRunner.BudgetFor(CommandStepLimits.ModelCheckMaxDuration);

        // The command's own timeout sits just under the budget, so the runner running out of time comes back as a result, not as the budget error.
        var set = new CommandSet([new BuildCommand("model-check", argv)], budget - TimeSpan.FromSeconds(1), OutputLimit);
        return await commands.Run("sindri step.model.check", set, workspace, budget, cancellationToken).ConfigureAwait(false);
    }

    private BlockStepResult Judge(CheckRun run, Judged judged)
    {
        if (run.ExitCode == 3 && !run.TimedOut)
        {
            return Findings("cantRun", [Tool(run.Tail.Trim())]);
        }

        var lines = TlcCheckLine.ParseAll(run.Tail);
        var found = new Found(judged.Runnable, lines, run);
        var findings = found.Violations().Concat(found.Witnesses()).Concat(judged.Rules).ToList();
        var specErrors = found.SpecErrors();
        var tooBig = found.TooBig(judged.Options.Witnesses, hasOther: findings.Count + specErrors.Count > 0);
        findings.AddRange(specErrors);
        findings.AddRange(tooBig);
        if (findings.Count == 0)
        {
            return run.ExitCode == 0 && !run.TimedOut ? Pass(judged.Runnable, lines, judged.Stale) : DidNotFinish(run);
        }

        return Findings(Outcome(lines, specErrors.Count + judged.Rules.Count > 0), findings);
    }

    private static string Outcome(List<TlcCheckLine> lines, bool specError) => lines.Exists(static l => l.Result == TlcResult.Violation) ? "violation"
        : lines.Exists(static l => l.Result is TlcResult.WitnessMissed or TlcResult.WitnessWrong) ? "vacuous"
        : specError ? "specError"
        : "tooBig";

    // Every printed line was a PASS but the runner did not finish clean (a folder it refused, a signal): no PASS counts.
    private BlockStepResult DidNotFinish(CheckRun run)
    {
        var how = run.TimedOut ? "ran past its time" : $"exited {run.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return Findings("specError", [new RuleFinding(SpecErrorRule, $"The model check didn't finish clean (the runner {how}) so no PASS counts. Sent back to fix it", Detail: run.Tail)]);
    }

    private BlockStepResult Findings(string outcome, IEnumerable<RuleFinding> findings) => StepFacts.Done(outcome, ("fact:findings", RuleFinding.ToFact(findings.Select(f => f.Scrubbed(scrubber)))));

    private RuleFinding Tool(string fix) => new(ToolRule, $"The model check can't run: {fix}. The PATH the step saw: {setup.Path}");

    private static BlockStepResult Pass(List<Spec> runnable, List<TlcCheckLine> lines, List<RuleFinding> stale)
    {
        var warnings = runnable.Where(static s => s.Readme.Bounds is null)
            .Select(static s => new RuleFinding(WarningRule, $"{s.Folder.Path}/README.md has no 'Bounds:' line; the cfg's CONSTANT lines stand in for it", $"{s.Folder.Path}/README.md"))
            .Concat(stale).ToList();
        var specs = new JsonArray([.. runnable.Select(spec => SpecFact(spec, lines))]);
        return StepFacts.Done(
            "pass",
            ("fact:modelCheck", new JsonObject { ["skipped"] = false, ["looked"] = Looked(runnable), ["specs"] = specs, ["warnings"] = RuleFinding.ToFact(warnings) }));
    }

    private static JsonObject SpecFact(Spec spec, List<TlcCheckLine> lines)
    {
        var name = NameOf(spec.Folder);
        var constants = Constants(spec.Folder);
        return new JsonObject
        {
            ["name"] = name,
            ["bounds"] = spec.Readme.Bounds ?? string.Join("; ", constants),
            ["constants"] = new JsonArray([.. constants.Select(static c => (JsonNode?)c)]),
            ["cfgs"] = new JsonArray([.. lines.Where(l => l.Name == name && l.Result == TlcResult.Pass).Select(static l => (JsonNode?)new JsonObject { ["cfg"] = l.Cfg, ["distinct"] = l.Distinct, ["depth"] = l.Depth })]),
            ["witnesses"] = new JsonArray([.. lines.Where(l => l.Name == name && l.Result == TlcResult.WitnessCaught).Select(static l => (JsonNode?)new JsonObject { ["cfg"] = l.Cfg, ["property"] = l.Rest.Split(' ').LastOrDefault() ?? string.Empty })]),
        };
    }

    private static List<string> Constants(SpecFolder folder) => [.. folder.Files
        .Where(static f => f.Kind == SpecFileKind.Regular && f.Name.EndsWith(".cfg", StringComparison.Ordinal) && !f.Name.Contains('/', StringComparison.Ordinal))
        .SelectMany(static f => f.Text.Split('\n'))
        .Select(static l => _constant.Match(l.TrimEnd('\r')))
        .Where(static m => m.Success)
        .Select(static m => $"{m.Groups["name"].Value} = {m.Groups["value"].Value}")
        .Distinct(StringComparer.Ordinal)];

    // ---- the step's own records

    private sealed record Request(string Workspace, ModelCheckOptions Options, List<Spec> All, List<Spec> Chosen);

    private sealed record Invocation(string Runner, string Workspace, List<Spec> Runnable, ModelCheckOptions Options);

    private sealed record Judged(List<Spec> Runnable, IReadOnlyList<RuleFinding> Rules, ModelCheckOptions Options, List<RuleFinding> Stale);
}
