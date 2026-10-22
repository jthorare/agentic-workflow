using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'recipe' verb group's table (spec §6.3; its text is Recipe/RecipeText). 'validate' and 'eval' are local: they
/// run in this process against the build's catalog and never start or contact serve; 'eval --record' reads an instance's events from the ledger
/// and the stored facts, so it needs no serve either. Checker exit codes (W27): 0 pass, 1 fail, 2 usage or tool error.
/// </summary>
internal static partial class RecipeCommandTable
{
    private const string ValidateUsage = "usage: sindri recipe validate <file> [--json] [--against <manifest.json>]";
    private const string EvalUsage = "usage: sindri recipe eval <file> [--json] | --shipped [--json] | --scaffold <file> [--out <dir>] | --record <address> --out <file>";
    private const string ApproveUsage = "usage: sindri recipe approve <file|id@hash>";
    private const string ApproveAllUsage = "usage: sindri recipe approve-all [--json]";
    private const string RevokeUsage = "usage: sindri recipe revoke <id@hash>";
    private const string RegisterUsage = "usage: sindri recipe register <file>";
    private const string ShowUsage = "usage: sindri recipe show <id|id@hash> [--json]";
    private const string ListUsage = "usage: sindri recipe list [--stale] [--json]";

    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => Run(args, services, output, desk: null, ct);

    /// <summary>Runs a 'recipe' verb; the registry's server verbs (register, show, list, approve, revoke) need <paramref name="desk"/>.</summary>
    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, RecipeDesk? desk, CancellationToken ct) => args switch
    {
        ["validate", .. var withBaseline] when withBaseline.Contains("--against", StringComparer.Ordinal) => ValidateAgainst(withBaseline, services, output, ct),
        ["validate", "--json"] or ["validate"] => output.Error(SindriErrors.InvalidInput.With($"recipe validate needs a recipe file ({ValidateUsage})")),
        ["validate", var file] => Validate(ValidateRecipeRequest.FromPath(file), json: false, services, output, ct),
        ["validate", var file, "--json"] => Validate(ValidateRecipeRequest.FromPath(file), json: true, services, output, ct),
        ["validate", "--json", var file] => Validate(ValidateRecipeRequest.FromPath(file), json: true, services, output, ct),
        ["eval", .. var shipped] when shipped.Contains("--shipped", StringComparer.Ordinal) => RunEvalShipped(shipped, services, output, ct),
        ["eval", .. var rest] => RunEval(rest, services, output, ct),
        ["manifest", .. var manifest] => RunManifest(manifest, services, output, ct),
        ["register", var file] when !IsFlag(file) && desk is not null => new RecipeRegistryCommands(desk, services, output).Register(file, ct),
        ["register", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe register needs one recipe file ({RegisterUsage})")),
        ["show", var target] when !IsFlag(target) && desk is not null => new RecipeRegistryCommands(desk, services, output).Show(target, json: false, ct),
        ["show", var target, "--json"] when !IsFlag(target) && desk is not null =>
            new RecipeRegistryCommands(desk, services, output).Show(target, json: true, ct),
        ["show", "--json", var target] when !IsFlag(target) && desk is not null =>
            new RecipeRegistryCommands(desk, services, output).Show(target, json: true, ct),
        ["show", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe show needs one id or id@hash and takes only --json ({ShowUsage})")),
        ["list", .. var flags] when ListFlags(flags) is { } list && desk is not null => new RecipeRegistryCommands(desk, services, output).List(list.Stale, list.Json, ct),
        ["list", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe list takes only --stale and --json ({ListUsage})")),
        ["approve-all"] when desk is not null => new ApproveAllCommand(desk, output).Run(json: false, ct),
        ["approve-all", "--json"] when desk is not null => new ApproveAllCommand(desk, output).Run(json: true, ct),
        ["approve-all", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe approve-all takes only --json ({ApproveAllUsage})")),
        ["approve", var target] when !IsFlag(target) && desk is not null => new RecipeDeskCommands(desk, services, output).Approve(target, ct),
        ["approve", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe approve needs one recipe file or id@hash ({ApproveUsage})")),
        ["revoke", var target] when target.Contains('@', StringComparison.Ordinal) && desk is not null => new RecipeDeskCommands(desk, services, output).Revoke(target, ct),
        ["revoke", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe revoke needs one id@hash ({RevokeUsage})")),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["recipe", .. args])}' is not a sindri command ({ValidateUsage}; {EvalUsage}; {ManifestUsage}; {RegisterUsage}; {ShowUsage}; {ListUsage}; {ApproveUsage}; {ApproveAllUsage}; {RevokeUsage})")),
    };

    private static Task<int> RunEval(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["--record", var address, "--out", var file] when !IsFlag(address) && !IsFlag(file) => RecordOut(address, file, services, output, ct),
        ["--record", ..] => output.Error(SindriErrors.InvalidInput.With($"recipe eval --record needs an address and --out <file> ({EvalUsage})")),
        [] or ["--json"] or ["--scaffold"] => output.Error(SindriErrors.InvalidInput.With($"recipe eval needs a recipe file ({EvalUsage})")),
        ["--scaffold", var file, "--out", var directory] when !IsFlag(file) && !IsFlag(directory) => ScaffoldOut(file, directory, services, output, ct),
        ["--scaffold", var file] when !IsFlag(file) => Eval(new EvalRecipeRequest(file, EvalMode.Scaffold), json: false, services, output, ct),
        ["--json", var file] when !IsFlag(file) => Eval(new EvalRecipeRequest(file, EvalMode.Run), json: true, services, output, ct),
        [var file, "--json"] when !IsFlag(file) => Eval(new EvalRecipeRequest(file, EvalMode.Run), json: true, services, output, ct),
        [var file] when !IsFlag(file) => Eval(new EvalRecipeRequest(file, EvalMode.Run), json: false, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["recipe", "eval", .. args])}' is not a sindri command ({EvalUsage})")),
    };

    /// <summary>'list' takes '--stale' and '--json', each at most once, in either order.</summary>
    private static (bool Stale, bool Json)? ListFlags(string[] flags) => flags switch
    {
        [] => (false, false),
        ["--stale"] => (true, false),
        ["--json"] => (false, true),
        ["--stale", "--json"] or ["--json", "--stale"] => (true, true),
        _ => null,
    };

    private static bool IsFlag(string arg) => arg.StartsWith("--", StringComparison.Ordinal);

    private static async Task<int> Validate(ValidateRecipeRequest request, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        Result<ValidateRecipeResponse> result;
        try
        {
            result = await services.GetRequiredService<IService<ValidateRecipeRequest, ValidateRecipeResponse>>()
                .Handle(request, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in reading or validation is a tool error (W27 exit 2), not a crash
        {
            return await ToolError(services, output, "recipe validate", e, request.Path ?? string.Empty).ConfigureAwait(false);
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

    private static async Task<int> Eval(EvalRecipeRequest request, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        Result<EvalRecipeResponse> result;
        try
        {
            result = await services.GetRequiredService<IService<EvalRecipeRequest, EvalRecipeResponse>>()
                .Handle(request, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in reading or folding is a tool error (W27 exit 2), not a crash
        {
            return await ToolError(services, output, "recipe eval", e, request.Path).ConfigureAwait(false);
        }

        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var response = result.Value;
        if (request.Mode == EvalMode.Scaffold)
        {
            return await output.Text(response.Scaffold ?? string.Empty, 0).ConfigureAwait(false);
        }

        var code = response.Passed ? 0 : 1;
        return json
            ? await output.Json(response, code).ConfigureAwait(false)
            : await output.Text(RecipeText.Eval(response), code).ConfigureAwait(false);
    }

    private static Task<int> ToolError(IServiceProvider services, Output output, string verb, Exception e, string file)
    {
        LogUnhandled(services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RecipeCommandTable)), e, verb, file);
        return output.Error(SindriErrors.ApiInternal.With(
            $"{verb} failed inside sindri: {e.GetType().Name}: {e.Message} (fix: this is a sindri bug; report it with the recipe file)"));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "SND-API-005 {Verb} {File} failed with an unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string verb, string file);
}
