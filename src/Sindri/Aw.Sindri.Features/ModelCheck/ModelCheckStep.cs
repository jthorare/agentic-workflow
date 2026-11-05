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
internal sealed class ModelCheckStep(ISpecFolders folders, IBranchChanges changes, RepoCommandRunner commands, ITextScrubber scrubber, ModelCheckSetup setup, IPacketDirectory packets) : IBlockStep, IBlockStepId
{
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
        _ = packets;   // red: the stale-path and move checks are not built yet
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

        var ran = await RunRunner(runner, request.Workspace, runnable, request.Options, cancellationToken).ConfigureAwait(false);
        return ran.IsError
            ? BlockStepResult.Failed($"step.model.check: {ran.Error.Message}")
            : Judge(ran.Value.Single().Run, new Judged(runnable, rules, request.Options));
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
        ("fact:modelCheck", new JsonObject { ["skipped"] = true, ["looked"] = Looked(all), ["specs"] = new JsonArray() }));

    private static JsonArray Looked(List<Spec> all) => new([.. all.SelectMany(static s => s.Readme.Models ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(static p => (JsonNode?)p)]);

    private async ValueTask<Result<IReadOnlyList<CheckResult>>> RunRunner(string runner, string workspace, List<Spec> runnable, ModelCheckOptions options, CancellationToken cancellationToken)
    {
        List<string> argv =
        [
            "env", "-i",
            $"PATH={setup.Path}", $"HOME={setup.Home}", $"AW_STATE_DIR={setup.StateDir}", $"TLC_SH={setup.TlcSh}", $"TLC_HEAP={setup.TlcHeap}", $"TMPDIR={setup.Tmp}",
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
            return Pass(judged.Runnable, lines);
        }

        var outcome = lines.Exists(static l => l.Result == TlcResult.Violation) ? "violation"
            : lines.Exists(static l => l.Result is TlcResult.WitnessMissed or TlcResult.WitnessWrong) ? "vacuous"
            : specErrors.Count + judged.Rules.Count > 0 ? "specError"
            : "tooBig";
        return Findings(outcome, findings);
    }

    private BlockStepResult Findings(string outcome, IEnumerable<RuleFinding> findings) => StepFacts.Done(outcome, ("fact:findings", RuleFinding.ToFact(findings.Select(f => f.Scrubbed(scrubber)))));

    private RuleFinding Tool(string fix) => new(ToolRule, $"The model check can't run: {fix}. The PATH the step saw: {setup.Path}");

    private static BlockStepResult Pass(List<Spec> runnable, List<TlcCheckLine> lines)
    {
        var warnings = runnable.Where(static s => s.Readme.Bounds is null)
            .Select(static s => new RuleFinding(WarningRule, $"{s.Folder.Path}/README.md has no 'Bounds:' line; the cfg's CONSTANT lines stand in for it", $"{s.Folder.Path}/README.md"))
            .ToList();
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

    // ---- what the runner said, read as findings

    private sealed record Request(string Workspace, ModelCheckOptions Options, List<Spec> All, List<Spec> Chosen);

    private sealed record Spec(SpecFolder Folder, ModelReadme Readme);

    private sealed record Judged(List<Spec> Runnable, IReadOnlyList<RuleFinding> Rules, ModelCheckOptions Options);

    private sealed class Found(List<Spec> runnable, List<TlcCheckLine> lines, CheckRun run)
    {
        internal IEnumerable<RuleFinding> Violations() => lines.Where(static l => l.Result == TlcResult.Violation).Select(l =>
        {
            var spec = SpecOf(l);
            var space = l.Rest.IndexOf(' ', StringComparison.Ordinal);
            var property = (space < 0 ? l.Rest : l.Rest[(space + 1)..]).Trim();
            var words = spec.Readme.Properties.TryGetValue(property, out var plain) ? plain.TrimEnd('.') : "a property of the design broke";
            return new RuleFinding(ViolationRule, $"The model found a hole: {words} ({property}, {l.Label})", CfgPath(spec, l), Detail: TraceSteps.Detail(l.Detail, spec.Readme.Vars));
        });

        internal IEnumerable<RuleFinding> Witnesses() => lines.Where(static l => l.Result is TlcResult.WitnessMissed or TlcResult.WitnessWrong).Select(l =>
        {
            var wrong = _wrong.Match(l.Rest);
            var how = l.Result == TlcResult.WitnessMissed
                ? "it was not caught"
                : wrong.Success ? $"it tripped {wrong.Groups["actual"].Value} instead of {wrong.Groups["expected"].Value}" : $"it tripped another property ({l.Rest})";
            return new RuleFinding(WitnessRule, $"The model didn't catch the bug planted to test it, so it proves nothing there: {how} ({l.Label}). Sent back to fix the model", CfgPath(SpecOf(l), l));
        });

        internal List<RuleFinding> SpecErrors()
        {
            var errors = lines.Where(l => l.Result == TlcResult.Error && !IsOutOfMemory(l))
                .Select(l => new RuleFinding(SpecErrorRule, $"The model doesn't check: {l.Label}: {l.Rest}. Sent back to fix it", CfgPath(SpecOf(l), l), Detail: l.Detail.Count == 0 ? null : string.Join('\n', l.Detail)))
                .ToList();
            if (lines.Count == 0 && run.ExitCode != 0 && !run.TimedOut)
            {
                errors.Add(new RuleFinding(SpecErrorRule, $"The model check printed no results (the runner exited {run.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}). Sent back to fix it", Detail: run.Tail));
            }

            return errors;
        }

        // One finding per spec: a check that ran past its time or out of memory, and the checks that never reported.
        internal List<RuleFinding> TooBig(bool witnesses, bool hasOther)
        {
            var cut = run.TimedOut || run.Tail.Length >= OutputLimit;
            var seen = lines.Select(static l => l.Label).ToHashSet(StringComparer.Ordinal);
            var findings = new List<RuleFinding>();
            foreach (var spec in runnable)
            {
                var name = NameOf(spec.Folder);
                var why = lines.Where(l => l.Name == name && (l.Result == TlcResult.Timeout || IsOutOfMemory(l)))
                    .Select(l => l.Result == TlcResult.Timeout ? $"{l.Label} ran past {CheckSeconds} s" : $"{l.Label} ran out of memory").ToList();
                if (cut || !hasOther)
                {
                    why.AddRange(Expected(spec, witnesses).Where(label => !seen.Contains(label)).Select(label => $"{label} never reported"));
                }

                if (why.Count > 0)
                {
                    findings.Add(new RuleFinding(TooBigRule, $"The model is too big to check: {string.Join(", ", why)} (bounds: {spec.Readme.Bounds ?? string.Join("; ", Constants(spec.Folder))}). Sent back to make the bounds smaller", spec.Folder.Path));
                }
            }

            return findings;
        }

        private static IEnumerable<string> Expected(Spec spec, bool witnesses)
        {
            var name = NameOf(spec.Folder);
            return spec.Folder.Files.Where(f => f.Kind == SpecFileKind.Regular && f.Name.EndsWith(".cfg", StringComparison.Ordinal) && (witnesses || !f.Name.StartsWith("gaps/", StringComparison.Ordinal)))
                .Select(f => $"{name}/{f.Name}");
        }

        private static bool IsOutOfMemory(TlcCheckLine line) => line.Rest.Contains("OutOfMemoryError", StringComparison.Ordinal) || line.Detail.Any(static d => d.Contains("OutOfMemoryError", StringComparison.Ordinal));

        private Spec SpecOf(TlcCheckLine line) => runnable.FirstOrDefault(s => string.Equals(NameOf(s.Folder), line.Name, StringComparison.Ordinal)) ?? runnable[0];

        private static string CfgPath(Spec spec, TlcCheckLine line) => $"{spec.Folder.Path}/{line.Cfg}.cfg";
    }
}
