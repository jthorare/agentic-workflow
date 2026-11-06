namespace Aw.Sindri.Core.Reviews;

/// <summary>The one-field login prompt shown while <c language="text">review.login</c> is unset (SND-REVIEW-007).</summary>
/// <param name="Code">SND-REVIEW-007.</param>
/// <param name="Text">"Set your GitHub login".</param>
/// <param name="Suggested">The forge credential's own login, or null when Sindri can't tell.</param>
public sealed record ReviewLoginPrompt(string Code, string Text, string? Suggested)
{
    /// <summary>The prompt for a suggested login.</summary>
    /// <param name="suggested">The credential's login, or null.</param>
    /// <returns>The prompt.</returns>
    public static ReviewLoginPrompt For(string? suggested) => new("SND-REVIEW-007", "Set your GitHub login", suggested);
}
