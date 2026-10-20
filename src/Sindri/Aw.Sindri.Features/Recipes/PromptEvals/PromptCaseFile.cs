namespace Aw.Sindri.Features.Recipes.PromptEvals;

/// <summary>A prompt eval's cases for one agent Step (SC-E): the seed under test, the baseline's Goal and schema, the judges and the cases.</summary>
/// <param name="Step">The block id of the Step.</param>
/// <param name="Goal">The Step's Goal, the baseline arm's whole instruction with <paramref name="OutputSchema"/>.</param>
/// <param name="OutputSchema">The output schema text the baseline arm names.</param>
/// <param name="Seed">The seed prompt under test.</param>
/// <param name="Judges">The binary judges the cases may name.</param>
/// <param name="Cases">The cases.</param>
internal sealed record PromptCaseFile(string Step, string Goal, string OutputSchema, string Seed, IReadOnlyList<PromptJudge> Judges, IReadOnlyList<PromptCase> Cases);

/// <summary>A binary judge: one failure mode, calibrated on labelled examples split into few-shot and held-out parts.</summary>
/// <param name="Id">The judge's id.</param>
/// <param name="Criterion">What a passing output satisfies.</param>
/// <param name="Examples">The few-shot examples shown to the grader.</param>
/// <param name="HeldOut">The held-out examples TPR and TNR are measured on; never shown to the grader.</param>
internal sealed record PromptJudge(string Id, string Criterion, IReadOnlyList<PromptExample> Examples, IReadOnlyList<PromptExample> HeldOut);

/// <summary>A labelled output.</summary>
/// <param name="Output">The output text.</param>
/// <param name="Pass">Whether the output satisfies the judge's criterion (the label).</param>
internal sealed record PromptExample(string Output, bool Pass);

/// <summary>One case: an input, and the graders its output must satisfy.</summary>
/// <param name="Id">The case id.</param>
/// <param name="Split">'tune' or 'heldOut' (the change the seed was never tuned on).</param>
/// <param name="Input">The case's input text, appended to the prompt.</param>
/// <param name="Checks">Deterministic graders.</param>
/// <param name="JudgeIds">Ids of the judges the output must also pass.</param>
internal sealed record PromptCase(string Id, string Split, string Input, IReadOnlyList<PromptCheck> Checks, IReadOnlyList<string> JudgeIds);

/// <summary>A deterministic grader: 'contains', 'notContains', 'equals', 'matches' (a regular expression) or 'isJson'.</summary>
/// <param name="Kind">The grader kind.</param>
/// <param name="Value">Its operand; empty for 'isJson'.</param>
internal sealed record PromptCheck(string Kind, string Value);
