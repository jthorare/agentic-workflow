using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.RegistryWrites;

/// <summary>
/// Registers a recipe by hash (engine spec §4.1, §6.3 'recipe register', §8, §9.1): reads and validates the text against this build's catalog and the registry view
/// (RCP-VERSION-001, -002 and, for a recipe that names a registered base, RCP-CAP-001 to -006 and RCP-CAP-003), with its child recipes resolved from the registered ones. A failing recipe is
/// SND-RECIPE-001 with every rule line, or SND-RECIPE-007 when its id and version are registered with other content, and records nothing. A passing one is stored as its normalized
/// long form, then 'recipe.registered' is appended; registering the same hash again adds nothing and returns the earlier event. Shared by RegisterRecipe and the approve-all job.
/// </summary>
/// <param name="reader">Reads recipe text.</param>
/// <param name="store">The registered recipes.</param>
/// <param name="registry">The validation context against the registry, and where the stored recipe stands.</param>
/// <param name="events">Appends 'recipe.registered'.</param>
/// <param name="json">The package's AwJson options (they write the normalized form).</param>
/// <param name="clock">When it was registered.</param>
public sealed class RecipeRegistrar(IRecipeReader reader, IRecipeStore store, RecipeRegistryReader registry, RecipeEvents events, JsonSerializerOptions json, TimeProvider clock)
{
    /// <summary>Registers the recipe in <paramref name="text"/>.</summary>
    /// <param name="text">The recipe file's text.</param>
    /// <param name="file">The file name its findings name.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>What was registered; SND-RECIPE-001 or SND-RECIPE-007 when it doesn't validate.</returns>
    public async ValueTask<Result<RegistrarOutcome>> Register(string text, string file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(file);
        var validated = await Validate(text, file, cancellationToken).ConfigureAwait(false);
        if (validated.IsError)
        {
            return validated.Error;
        }

        var recipe = validated.Value.Recipe;
        var normalized = Encoding.UTF8.GetString(AwJson.Serialize(recipe with { Hash = null }, json));
        var stored = await store.Add(new RegisteredRecipe(recipe.Id, recipe.Version, validated.Value.Hash, normalized, recipe.Base, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        var appended = stored.IsError ? stored.Error : await events.Registered(stored.Value.Recipe, cancellationToken).ConfigureAwait(false);
        var line = appended.IsError ? appended.Error : await registry.Summary(stored.Value.Recipe, cancellationToken).ConfigureAwait(false);
        return line.IsError
            ? line.Error
            : new RegistrarOutcome(recipe.Id, recipe.Version, validated.Value.Hash, stored.Value.Added, line.Value.Standing, appended.Value.Sequence);
    }

    /// <summary>The bound document when it passes against the registry; else SND-RECIPE-007 for RCP-VERSION-001, or SND-RECIPE-001.</summary>
    private async ValueTask<Result<RecipeDocument>> Validate(string text, string file, CancellationToken cancellationToken)
    {
        var reading = reader.Read(text, file);
        if (reading.Document is not { } document)
        {
            return Refused(file, reading.Findings);
        }

        var context = await registry.Validation(document.Recipe, cancellationToken).ConfigureAwait(false);
        if (context.IsError)
        {
            return context.Error;
        }

        List<RecipeFinding> findings = [.. reading.Findings, .. RecipeValidator.Validate(document, context.Value)];
        return RecipeValidator.Passes(findings) ? document : Refused(file, findings);
    }

    private static Error Refused(string file, IEnumerable<RecipeFinding> findings)
    {
        var errors = findings.Where(static f => f.Severity == FindingSeverity.Error).ToList();
        var lines = string.Concat(errors.Select(static f => "\n  " + f.ToLine()));
        return errors.Exists(static f => f.Rule == "RCP-VERSION-001")
            ? WorkflowErrors.RecipeVersionTaken.With($"{file}'s id and version are registered with other content; nothing was registered:{lines}")
            : WorkflowErrors.RecipeInvalid.With($"{file} doesn't validate; nothing was registered. Fix each line, then register again:{lines}");
    }
}
