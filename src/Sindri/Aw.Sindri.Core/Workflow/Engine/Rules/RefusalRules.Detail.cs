using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>What a refusal says (the park's question, §3.8) and what it marks for the no-progress check (§3.7).</summary>
internal static partial class RefusalRules
{
    /// <summary>
    /// The refusal's digest for the no-progress check (A §17.23.13 'sameFailure'): the SHA-256 of the refusing gates'
    /// and the failed evidence checks' reasons, one per line. A re-run that gets the same reasons made no progress.
    /// </summary>
    internal static ProgressMark Mark(RefusalInput input)
    {
        var reasons = input.Gates.Where(g => g.Outcome != GateOutcome.Passed).SelectMany(g => g.Reasons)
            .Concat(input.Evidence.Where(e => !e.Held).SelectMany(e => e.Reasons));
        return new ProgressMark(Findings: null, Digest(string.Join('\n', reasons)), EmptyDiff: false);
    }

    /// <summary>The SHA-256 of <paramref name="text"/> as lower-case hex.</summary>
    internal static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>'{gate} refused {from} -> {to} ({reasons})', naming the first gate or evidence check that refused.</summary>
    private static string Describe(RefusalInput input)
    {
        var move = Move(input);
        if (input.Gates.FirstOrDefault(g => g.Outcome != GateOutcome.Passed) is { } gate)
        {
            return $"{gate.Gate.Value} refused {move} ({Reasons(gate.Reasons)})";
        }

        var evidence = input.Evidence.FirstOrDefault(e => !e.Held);
        return evidence is null
            ? $"{move} was refused"
            : $"evidence {evidence.Kind}@{Point(evidence.At)} didn't {(evidence.Expect == EvidenceExpectation.Pass ? "pass" : "fail")} for {move} ({Reasons(evidence.Reasons)})";
    }

    private static string Move(RefusalInput input) => $"{input.Instance.State} -> {input.Recipe.Transitions[input.Transition].To}";

    private static string Reasons(IReadOnlyList<string> reasons) => reasons.Count == 0 ? "no reasons given" : string.Join("; ", reasons);

    private static string Score(double? value) => value is { } v ? v.ToString("0.###", CultureInfo.InvariantCulture) : "none";

    private static string Point(EvidencePoint at) => at == EvidencePoint.Base ? "base" : "head";

    /// <summary>A refused transition with what the human reads about it.</summary>
    /// <param name="Input">The refused transition.</param>
    /// <param name="Detail">The park's question.</param>
    private sealed record Refusal(RefusalInput Input, string Detail)
    {
        /// <summary>Whether the instance is in a wait (§5.5 step 4).</summary>
        public bool FromWait => Input.Recipe.States.TryGetValue(Input.Instance.State, out var state) && state.Kind == StateKind.Wait;

        /// <summary>'{from} -> {to}'.</summary>
        public string Move => RefusalRules.Move(Input);

        /// <summary>
        /// The resume that checks the transition again (§5.8): reevaluate the withheld trigger, or, for a resource
        /// grant (never a stored trigger), wait for the resource again.
        /// </summary>
        public ResumeAction Withhold() => Input.Trigger is WaitSatisfied { Source: ResourceWaitSource }
            ? new ResumeAction(ResumeKind.Rewait, Withheld: null, Gate: null, Effects: [])
            : new ResumeAction(ResumeKind.Reevaluate, new WithheldTrigger(Input.Transition, Input.Trigger), Gate: null, Effects: []);
    }
}
