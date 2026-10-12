using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Features.ValidateRecipe;

/// <summary>
/// Reads the recipe (a file, or text with the name findings show), validates it against the build's catalog, and
/// returns the reader's and the validator's findings with the hash. Not served, a child recipe resolves from the
/// recipe file's directory (spec §9.1); text has no directory, so its children don't resolve. The profile has no
/// effect allowlist yet (S1-D11, P6), so RCP-EFFECT-001 is skipped.
/// </summary>
internal sealed class ValidateRecipeService(IRecipeReader reader, IRecipeFiles files, BlockCatalog catalog)
    : IService<ValidateRecipeRequest, ValidateRecipeResponse>
{
    public async ValueTask<Result<ValidateRecipeResponse>> Handle(ValidateRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = await Source(request, cancellationToken).ConfigureAwait(false);
        if (source.IsError)
        {
            return source.Error;
        }

        var reading = reader.Read(source.Value.Text, source.Value.File);
        if (reading.Document is not { } document)
        {
            return new ValidateRecipeResponse(reading.Findings, Hash: null);
        }

        var children = request.Path is { } path
            ? await SameDirectoryChildren.Load(path, document.Recipe, reader, files, cancellationToken).ConfigureAwait(false)
            : SameDirectoryChildren.None;
        var context = new ValidationContext(catalog, RecipeProfileFacts.None, children, BaseRecipe: null);
        return new ValidateRecipeResponse([.. reading.Findings, .. RecipeValidator.Validate(document, context)], document.Hash)
        {
            Normalized = document.Recipe,
        };
    }

    private async ValueTask<Result<(string Text, string File)>> Source(ValidateRecipeRequest request, CancellationToken cancellationToken)
    {
        if (request is { Path: { } path, Text: null })
        {
            var text = await files.Read(path, cancellationToken).ConfigureAwait(false);
            return text.IsError ? text.Error : Results.Success((text.Value, Path.GetFileName(path)));
        }

        return request is { Path: null, Text: { } given }
            ? Results.Success((given, request.File ?? "recipe.yaml"))
            : KernelErrors.Invalid.With("Give either a recipe file's path or its text, not both or neither.");
    }
}
