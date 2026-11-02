namespace Aw.Context.Core.Engine;

/// <summary>One step of a call's route: the harness to ask, the model to ask it for, and where that name came from.</summary>
/// <param name="Harness">The harness.</param>
/// <param name="Model">The model name (always passes <see cref="ModelNames.IsValid"/> unless the request named it).</param>
/// <param name="Source">Where the name came from.</param>
public sealed record RoutedModel(ModelHarness Harness, string Model, ModelSource Source);
