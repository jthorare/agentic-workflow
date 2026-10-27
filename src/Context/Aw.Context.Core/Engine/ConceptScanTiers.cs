using Aw.Context.Core.Models;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The size at which a concept scan needs the strong model (owner, q121): above either threshold the scan asks the strong tier, otherwise the cheap one.
/// </summary>
/// <param name="StrongAboveFeatures">The most features of the map a cheap scan takes.</param>
/// <param name="StrongAbovePromptTokens">The most estimated tokens in the first prompt a cheap scan takes.</param>
public sealed record ConceptScanTiers(int StrongAboveFeatures, int StrongAbovePromptTokens)
{
    /// <summary>150 features or 10,000 prompt tokens (of the 15,000 the input cap allows); the profile's 'context.conceptScan' overrides them.</summary>
    public static ConceptScanTiers Default { get; } = new(150, 10_000);

    /// <summary>The tokens of a prompt, estimated the way the model port does: characters / 4, rounded up.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The estimate.</returns>
    public static int EstimateTokens(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return (prompt.Length + 3) / 4;
    }

    /// <summary>The tier for this much work.</summary>
    /// <param name="features">How many features the map summary names.</param>
    /// <param name="promptTokens">The first prompt's estimated tokens.</param>
    /// <returns>Strong when either is above its threshold, else cheap.</returns>
    public ModelTier Pick(int features, int promptTokens) =>
        features > StrongAboveFeatures || promptTokens > StrongAbovePromptTokens ? ModelTier.Strong : ModelTier.Cheap;
}
