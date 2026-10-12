using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// What the reader made of a recipe file: the bound document, or the findings that stopped it binding
/// (RCP-SCHEMA-001–003: refused YAML, an unknown or missing field, a value of the wrong type). The reader reports
/// every schema finding, not just the first.
/// </summary>
/// <param name="Document">The bound recipe, its hash and positions; null when it didn't bind.</param>
/// <param name="Findings">The reader's findings; empty when it bound cleanly.</param>
public sealed record RecipeReading(RecipeDocument? Document, IReadOnlyList<RecipeFinding> Findings);
