using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'recipe' verb group's table (spec §6.3; its text is Recipe/RecipeText). 'validate' is local: it runs in this
/// process against the build's catalog and never starts or contacts serve. Checker exit codes (W27): 0 pass, 1 fail,
/// 2 usage or tool error. 'eval' arrives with P3 (recipe evals); until then it refuses with exit 2.
/// </summary>
internal static partial class RecipeCommandTable
{
    private const string ValidateUsage = "usage: sindri recipe validate <file> [--json]";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["validate", "--json"] or ["validate"] => output.Error(SindriErrors.InvalidInput.With($"recipe validate needs a recipe file ({ValidateUsage})")),
        ["validate", var file] => Validate(file, json: false, services, output, ct),
        ["validate", var file, "--json"] => Validate(file, json: true, services, output, ct),
        ["validate", "--json", var file] => Validate(file, json: true, services, output, ct),
        ["eval", ..] => output.Error(SindriErrors.UnknownVerb.With(
            "'recipe eval' isn't in this build yet: recipe evals arrive with P3 (fix: check the recipe with sindri recipe validate <file>)")),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["recipe", .. args])}' is not a sindri command ({ValidateUsage})")),
    };

    private static async Task<int> Validate(string file, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        Result<ValidateRecipeResponse> result;
        try
        {
            result = await services.GetRequiredService<IService<ValidateRecipeRequest, ValidateRecipeResponse>>()
                .Handle(ValidateRecipeRequest.FromPath(file), ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in reading or validation is a tool error (W27 exit 2), not a crash
        {
            LogUnhandled(services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RecipeCommandTable)), e, file);
            return await output.Error(SindriErrors.ApiInternal.With(
                $"recipe validate failed inside sindri: {e.GetType().Name}: {e.Message} (fix: this is a sindri bug; report it with the recipe file)")).ConfigureAwait(false);
        }

        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var response = result.Value;
        var code = response.Passed ? 0 : 1;
        return json
            ? await output.Json(RecipeValidationView.From(response, services.GetRequiredService<JsonSerializerOptions>()), code).ConfigureAwait(false)
            : await output.Text(RecipeText.Validation(response), code).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "SND-API-005 recipe validate {File} failed with an unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string file);
}
