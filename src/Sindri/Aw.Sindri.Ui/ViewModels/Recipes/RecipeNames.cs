using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>
/// The plain names and one-line descriptions of the workflows that ship with Sindri, until a recipe carries a title and a description of its own
/// (recipes spec §4). A served display name wins; an id this map doesn't know reads through <see cref="WorkflowNames"/>, never as the raw id.
/// </summary>
internal static class RecipeNames
{
    private static readonly Dictionary<string, (string Name, string Does)> _known = new(StringComparer.Ordinal)
    {
        ["workflow.line"] = ("Idea to merged pull request", "The whole pipeline: scope, design, plan, then build and prove each phase."),
        ["workflow.scoping"] = ("Scoping", "Decide what will change, from a brief or a ticket."),
        ["workflow.idea-to-brief"] = ("Idea to brief", "Turn a rough idea typed in the app into a cited brief."),
        ["workflow.design"] = ("Design", "Turn the locked scope into one reviewed spec."),
        ["workflow.plan"] = ("Plan", "Split the design into buildable phases and check a builder could do them."),
        ["workflow.phase"] = ("Build and prove", "Build, prove, review and ship one phase as one pull request."),
        ["workflow.build"] = ("Build", "One test-first builder for one phase, in its own worktree."),
        ["workflow.prove"] = ("Prove", "Prove every acceptance item of a phase on its final commit."),
        ["workflow.ship"] = ("Ship", "Check the change is green, then open the pull request."),
        ["workflow.review-loop"] = ("Review loop", "Review a phase's change one angle at a time, round by round."),
        ["workflow.pr-babysitter"] = ("Pull request babysitter", "Placeholder until the babysitter lands: one step done by a person."),
        ["workflow.step"] = ("Plain step", "One step with a checks-and-revise loop; used for trying workflows out."),
    };

    /// <summary>The name of a workflow on the Recipes page.</summary>
    /// <param name="recipeId">The recipe id.</param>
    /// <param name="displayName">The served display name, or null.</param>
    /// <returns>The served name, else the mapped one, else the id made readable.</returns>
    internal static string Of(string recipeId, string? displayName)
    {
        ArgumentNullException.ThrowIfNull(recipeId);
        return !string.IsNullOrWhiteSpace(displayName) && !string.Equals(displayName.Trim(), recipeId, StringComparison.Ordinal)
            ? displayName
            : _known.TryGetValue(recipeId, out var known) ? known.Name : WorkflowNames.Of(recipeId, displayName: null);
    }

    /// <summary>What a workflow does, in a sentence, when this build knows it.</summary>
    /// <param name="recipeId">The recipe id.</param>
    /// <returns>The sentence, or empty.</returns>
    internal static string Does(string recipeId)
    {
        ArgumentNullException.ThrowIfNull(recipeId);
        return _known.TryGetValue(recipeId, out var known) ? known.Does : string.Empty;
    }
}
