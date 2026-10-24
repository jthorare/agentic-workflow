namespace Aw.Sindri.Ui.Testing;

/// <summary>One recipe approval the fake received: the recipe and the hash it was sent for.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash in the route.</param>
internal sealed record RecipeApprovalSent(string Id, string Hash);
