namespace Aw.Sindri.Core.Setup;

/// <summary>The git tree hash of 'recipes/sindri' as this build embeds it, and as the checkout the app runs from holds it at HEAD (setup-once spec §5.3).</summary>
/// <param name="Embedded">The tree hash recorded in the build's stamp.</param>
/// <param name="Head">The checkout's HEAD tree hash, or null when the app isn't run from a checkout that has the folder.</param>
public sealed record RecipeTrees(string Embedded, string? Head);
