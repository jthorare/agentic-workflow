namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/workflow/recipes/{id}@{hash}/approve's answer: the accepted command, and whether the recipe already stood approved. The approval row and the resumed instances are not read.</summary>
/// <param name="Accepted">The command id and the ledger sequence of 'recipe.approved'.</param>
/// <param name="AlreadyApproved">True when the recipe already stood approved with these pins.</param>
internal sealed record RecipeApproved(CommandAcceptedView Accepted, bool AlreadyApproved = false);
