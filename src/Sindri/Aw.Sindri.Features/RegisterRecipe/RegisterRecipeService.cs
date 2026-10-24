using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryWrites;

namespace Aw.Sindri.Features.RegisterRecipe;

/// <summary>
/// Registers a recipe by hash (engine spec §4.1, §6.3 'recipe register', §8, §9.1; desk) through <see cref="RecipeRegistrar"/>, the registration the approve-all job shares: a failing recipe is
/// SND-RECIPE-001 with every rule line, or SND-RECIPE-007 when its id and version are registered with other content, and records nothing; registering the same hash again adds nothing and returns the earlier event.
/// </summary>
/// <param name="reader">Reads recipe text.</param>
/// <param name="store">The registered recipes.</param>
/// <param name="registry">The validation context against the registry, and where the stored recipe stands.</param>
/// <param name="events">Appends 'recipe.registered'.</param>
/// <param name="json">The package's AwJson options (they write the normalized form).</param>
/// <param name="clock">When it was registered.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class RegisterRecipeService(
    IRecipeReader reader,
    IRecipeStore store,
    RecipeRegistryReader registry,
    RecipeEvents events,
    JsonSerializerOptions json,
    TimeProvider clock,
    ICallerContextAccessor callers)
    : IService<RegisterRecipeRequest, RegisterRecipeResponse>
{
    public async ValueTask<Result<RegisterRecipeResponse>> Handle(RegisterRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("RegisterRecipe needs a caller");
        }

        var done = await new RecipeRegistrar(reader, store, registry, events, json, clock).Register(request.Text, request.File, cancellationToken).ConfigureAwait(false);
        return done.IsError
            ? done.Error
            : new RegisterRecipeResponse(new CommandAccepted(caller.CommandId, done.Value.Sequence), done.Value.Id, done.Value.Version, done.Value.Hash, done.Value.Added, done.Value.Standing);
    }
}
