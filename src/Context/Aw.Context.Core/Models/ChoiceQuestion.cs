namespace Aw.Context.Core.Models;

/// <summary>One question for <see cref="IChoicePort"/>: pick one of two to five options. Every string is already scrubbed.</summary>
/// <param name="Key">The key the answer comes back under (a cluster id for labeling); unique within a call.</param>
/// <param name="Prompt">What is asked, with the data it is about (for labeling, the cluster's scrubbed paths).</param>
/// <param name="Options">The options, <see cref="LabelPickLimits.MinOptions"/> to <see cref="LabelPickLimits.MaxOptions"/> of them, none twice.</param>
public sealed record ChoiceQuestion(string Key, string Prompt, IReadOnlyList<string> Options);
