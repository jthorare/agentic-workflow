using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>One recipe approval the fake received: the recipe, the hash it was sent for and the answer.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash in the route.</param>
/// <param name="Answer">The challenge and the typed text.</param>
internal sealed record RecipeApprovalSent(string Id, string Hash, ProfileAnswer Answer);
