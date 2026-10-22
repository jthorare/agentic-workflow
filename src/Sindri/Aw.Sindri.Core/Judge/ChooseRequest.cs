namespace Aw.Sindri.Core.Judge;

/// <summary>What 'step.judge.choose' asks the judge (DT spec 3.1, 8.3). The text is already redacted and capped.</summary>
/// <param name="Prompt">The question.</param>
/// <param name="Choices">The options, two to 32.</param>
/// <param name="Facts">The facts the question reads, by fact key, as text.</param>
/// <param name="Threshold">The confidence an answer needs, above 0 and at most 1.</param>
/// <param name="Providers">The providers the judge may ask, in order (the effective allowlist, 'rules' last).</param>
public sealed record ChooseRequest(string Prompt, IReadOnlyList<ChooseOption> Choices, IReadOnlyDictionary<string, string> Facts, double Threshold, IReadOnlyList<string> Providers);
