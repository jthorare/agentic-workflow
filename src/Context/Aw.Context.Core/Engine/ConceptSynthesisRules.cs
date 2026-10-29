using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Reads and checks 'context.conceptSynthesis'. Every refusal is SND-CONTEXT-001 with the key path first.</summary>
internal static class ConceptSynthesisRules
{
    /// <summary>The settings of a section; a missing section or key is its default.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The settings.</returns>
    internal static ConceptSynthesisSettings Read(ConceptSynthesisSection? section) => Prompt(section, Grouping(section, new ConceptSynthesisSettings()));

    private static ConceptSynthesisSettings Grouping(ConceptSynthesisSection? section, ConceptSynthesisSettings defaults) => defaults with
    {
        Enabled = section?.Enabled ?? defaults.Enabled,
        Neighbours = section?.Neighbours ?? defaults.Neighbours,
        MinCosine = section?.MinCosine is { } cosine ? (float)cosine : defaults.MinCosine,
        MaxGroup = section?.MaxGroup ?? defaults.MaxGroup,
    };

    private static ConceptSynthesisSettings Prompt(ConceptSynthesisSection? section, ConceptSynthesisSettings read) => read with
    {
        MinRecords = section?.MinRecords ?? read.MinRecords,
        MinSources = section?.MinSources ?? read.MinSources,
        MaxCharsPerRecord = section?.MaxCharsPerRecord ?? read.MaxCharsPerRecord,
        MaxCharsPerCall = section?.MaxCharsPerCall ?? read.MaxCharsPerCall,
    };

    /// <summary>The first value out of range, or null.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>SND-CONTEXT-001 naming the key path, or null.</returns>
    internal static Error? Problem(ConceptSynthesisSection? section) => section switch
    {
        null => null,
        { Neighbours: < 1 or > 100 } => Refuse("neighbours", "is 1 to 100 records"),
        { MinCosine: < 0.1 or > 0.99 } => Refuse("minCosine", "is a cosine from 0.1 to 0.99"),
        { MaxGroup: < 2 or > 200 } => Refuse("maxGroup", "is 2 to 200 records"),
        _ => PromptProblem(section),
    };

    private static Error? PromptProblem(ConceptSynthesisSection section) => section switch
    {
        { MinRecords: < 2 or > 200 } => Refuse("minRecords", "is 2 to 200 records"),
        { MinSources: < 1 or > 10 } => Refuse("minSources", "is 1 to 10 sources"),
        { MaxCharsPerRecord: < 100 or > 20_000 } => Refuse("maxCharsPerRecord", "is 100 to 20000 characters"),
        { MaxCharsPerCall: < 1_000 or > 200_000 } => Refuse("maxCharsPerCall", "is 1000 to 200000 characters"),
        _ => null,
    };

    private static Error Refuse(string key, string problem) => SettingsReader.Invalid("context.conceptSynthesis." + key, problem);
}
