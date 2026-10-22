using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The app's own copy (<see cref="Labels"/>, continued): the Recipes page, shown to people as workflows (recipes spec §6). The page's title is ONE constant
/// (<see cref="RecipesPageTitle"/>) and the nav entry's name another (<see cref="Recipes"/>), so renaming either is a one-line change: the rail's Workflows means runs.
/// </summary>
internal static partial class Labels
{
    /// <summary>The Recipes page's title (the nav entry's own name is <see cref="Recipes"/>).</summary>
    internal const string RecipesPageTitle = "Workflows";

    /// <summary>The page's caption under the title.</summary>
    /// <param name="total">How many workflows are listed.</param>
    /// <param name="approved">How many of them are approved.</param>
    /// <returns>"12 workflows · 11 approved".</returns>
    internal static string RecipesCaption(int total, int approved) =>
        string.Create(CultureInfo.InvariantCulture, $"{total} {(total == 1 ? "workflow" : "workflows")} · {approved} approved");

    /// <summary>The standing word of an approved workflow.</summary>
    internal const string StandingApproved = "Approved";

    /// <summary>The standing word of a workflow that is not approved.</summary>
    internal const string StandingNotApproved = "Not approved";

    /// <summary>The standing word of a revoked workflow.</summary>
    internal const string StandingRevoked = "Revoked";

    /// <summary>The second, grey word of an approved workflow that has changed since.</summary>
    internal const string OutOfDate = "Out of date";

    /// <summary>The word for a standing.</summary>
    /// <param name="standing">The standing.</param>
    /// <returns>"Approved", "Not approved" or "Revoked".</returns>
    internal static string StandingWord(RecipeStanding standing) => standing switch
    {
        RecipeStanding.Approved => StandingApproved,
        RecipeStanding.Revoked => StandingRevoked,
        RecipeStanding.NotApproved => StandingNotApproved,
        _ => throw new ArgumentOutOfRangeException(nameof(standing), standing, message: null),
    };

    /// <summary>Why a workflow is out of date, in words.</summary>
    /// <param name="reason">The served reason: pin-drift, child-stale or legacy-shape.</param>
    /// <returns>The sentence (a reason this build doesn't know reads "It needs a look").</returns>
    internal static string StaleReason(string reason) => reason switch
    {
        "pin-drift" => "A step it uses changed since it was approved",
        "child-stale" => "A workflow it starts is out of date",
        "legacy-shape" => "It is written in an old format",
        _ => "It needs a look",
    };

    /// <summary>The Workflow column's header.</summary>
    internal const string ColWorkflow = "Workflow";

    /// <summary>The Version column's header.</summary>
    internal const string ColVersion = "Version";

    /// <summary>The Standing column's header.</summary>
    internal const string ColStanding = "Standing";

    /// <summary>The Size column's header.</summary>
    internal const string ColSize = "Size";

    /// <summary>The needs-and-produces column's header.</summary>
    internal const string ColFlow = "Needs and produces";

    /// <summary>The Fingerprint column's header.</summary>
    internal const string ColFingerprint = "Fingerprint";

    /// <summary>A workflow's size.</summary>
    /// <param name="steps">How many steps it has.</param>
    /// <returns>"8 steps".</returns>
    internal static string StepsWord(int steps) => string.Create(CultureInfo.InvariantCulture, $"{steps} {(steps == 1 ? "step" : "steps")}");

    /// <summary>What a workflow needs and what it produces, in counts.</summary>
    /// <param name="needs">How many inputs.</param>
    /// <param name="produces">How many outputs.</param>
    /// <returns>"2 in, 4 out".</returns>
    internal static string FlowWord(int needs, int produces) => string.Create(CultureInfo.InvariantCulture, $"{needs} in, {produces} out");

    /// <summary>A recipe's fingerprint: the first eight hex digits of its hash.</summary>
    /// <param name="hash">The hash as served.</param>
    /// <returns>Eight hex digits (fewer when the hash is short).</returns>
    internal static string Fingerprint(string hash)
    {
        var twelve = Hash12(hash);
        return twelve.Length > 8 ? twelve[..8] : twelve;
    }

    /// <summary>A workflow row's accessible name.</summary>
    /// <param name="name">The plain name.</param>
    /// <param name="id">The recipe id.</param>
    /// <param name="standing">The standing word.</param>
    /// <param name="outOfDate">True when the workflow is out of date.</param>
    /// <param name="version">The version.</param>
    /// <returns>"Build and prove, workflow.phase, Approved, version 2".</returns>
    internal static string RecipeRowName(string name, string id, string standing, bool outOfDate, int version) =>
        string.Create(CultureInfo.InvariantCulture, $"{name}, {id}, {standing}, {(outOfDate ? "out of date, " : string.Empty)}version {version}");

    /// <summary>The filter that shows every workflow.</summary>
    internal const string RecipesFilterAll = "All";

    /// <summary>The filter that shows the workflows that are not approved, revoked or out of date.</summary>
    internal const string RecipesFilterNeeds = "Needs approval";

    /// <summary>A filter's label with its count.</summary>
    /// <param name="label">The filter's words.</param>
    /// <param name="count">How many workflows it shows.</param>
    /// <returns>"All 12".</returns>
    internal static string RecipesFilterWithCount(string label, int count) => string.Create(CultureInfo.InvariantCulture, $"{label} {count}");

    /// <summary>The search field's hint.</summary>
    internal const string RecipesSearch = "Search workflows";

    /// <summary>The button that leads to where all new workflows are approved at once.</summary>
    internal const string ApproveNewWorkflows = "Approve new workflows";

    /// <summary>The first read.</summary>
    internal const string ReadingRecipes = "Reading workflows…";

    /// <summary>The box for a list with no workflows.</summary>
    internal const string RecipesEmpty = "No workflows are installed";

    /// <summary>The box for a filter or search that matches nothing.</summary>
    internal const string RecipesNoMatch = "No workflows match";

    /// <summary>The line under the table.</summary>
    internal const string RecipesFooter = "Only approved workflows can start a run. The run figures cover the window chosen above.";

    /// <summary>What the page says when its first read failed.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The sentence.</returns>
    internal static string RecipesFailed(Error error) => "Couldn't read the workflows. " + InstanceRefused(error);

    /// <summary>The sheet's callout for a workflow that is not approved.</summary>
    internal const string RecipeNotApprovedCallout = "This workflow is not approved, so it cannot start a run yet.";

    /// <summary>The sheet's callout for a revoked workflow.</summary>
    internal const string RecipeRevokedCallout = "This workflow was revoked, so it cannot start a run.";

    /// <summary>The sheet's callout for a workflow that is out of date.</summary>
    internal const string RecipeOutOfDateCallout = "This workflow is out of date.";

    /// <summary>The line above the command that approves one workflow.</summary>
    internal const string RecipeApproveHint = "To approve it, run this in a terminal:";

    /// <summary>The terminal form that approves one workflow (the app approves nothing here).</summary>
    /// <param name="recipeId">The recipe id.</param>
    /// <returns>The command.</returns>
    internal static string RecipeApproveCommand(string recipeId) => "sindri recipe approve " + recipeId;
}
