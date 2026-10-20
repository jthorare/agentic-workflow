namespace Aw.Sindri.Features.Recipes.PromptEvals;

/// <summary>The deterministic graders (SC-E 'check:').</summary>
internal static class PromptChecks
{
    /// <summary>Whether <paramref name="output"/> satisfies <paramref name="check"/>.</summary>
    /// <param name="check">The grader.</param>
    /// <param name="output">The model's output.</param>
    /// <returns>True when it passes.</returns>
    public static bool Passes(PromptCheck check, string output) => throw new NotSupportedException();
}
