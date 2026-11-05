using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Features.ModelCheck;

internal sealed partial class ModelCheckStep
{
    // ---- what the runner said, read as findings

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

        private static string CfgPath(Spec spec, TlcCheckLine line) => line.Cfg.Length == 0 ? spec.Folder.Path : $"{spec.Folder.Path}/{line.Cfg}.cfg";
    }
}
