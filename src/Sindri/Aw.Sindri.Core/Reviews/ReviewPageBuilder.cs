namespace Aw.Sindri.Core.Reviews;

/// <summary>The pure step from the page's facts to its model (PR review page spec §6.2, §6.3): header, strip, panels in the settings' order, and the grouped diff.</summary>
public static class ReviewPageBuilder
{
    /// <summary>Builds the page.</summary>
    /// <param name="facts">What was read on one head.</param>
    /// <returns>The page model.</returns>
    public static ReviewPageModel Build(ReviewPageFacts facts) => throw new NotSupportedException();
}
