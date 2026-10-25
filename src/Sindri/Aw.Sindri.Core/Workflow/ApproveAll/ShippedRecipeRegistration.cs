using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryWrites;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// Registers a shipped version on its way to an individual approval (#983). A shipped version that gains a capability waits for its own typed approval, and approve-all leaves it out, so
/// nothing registered it: the single approval, 'approve &lt;id&gt;@&lt;hash&gt;' or 'approve &lt;id&gt; &lt;version&gt;', registers it from this build's embedded set first, as approve-all does for its rows.
/// Only a version this build ships is registered; any other id, version or hash is left for the planner to refuse as not found.
/// </summary>
/// <param name="shipped">The shipped recipes.</param>
/// <param name="registrar">Registers a recipe.</param>
/// <param name="store">The registered recipes.</param>
public sealed class ShippedRecipeRegistration(ShippedRecipeSet shipped, RecipeRegistrar registrar, IRecipeStore store)
{
    /// <summary>Registers the shipped version of <paramref name="id"/> that <paramref name="hash"/> names (whole, or a prefix of 8 or more digits), unless it is registered.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash or its prefix.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Success when nothing was needed or the version was registered; the registration's refusal otherwise.</returns>
    public ValueTask<Result<bool>> EnsureHash(RecipeId id, string hash, CancellationToken cancellationToken) =>
        Ensure(id, file => string.Equals(file.Hash.Value, hash, StringComparison.Ordinal) || file.Hash.MatchesPrefix(hash), cancellationToken);

    /// <summary>Registers the shipped version <paramref name="version"/> of <paramref name="id"/>, unless a version of that number is registered.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">The version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Success when nothing was needed or the version was registered; the registration's refusal otherwise.</returns>
    public async ValueTask<Result<bool>> EnsureVersion(RecipeId id, int version, CancellationToken cancellationToken)
    {
        var registered = await store.Versions(id, cancellationToken).ConfigureAwait(false);
        if (registered.IsError || registered.Value.Any(r => r.Version == version))
        {
            return registered.IsError ? registered.Error : false;
        }

        return await Ensure(id, file => file.Version == version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Registers the shipped file of <paramref name="id"/> that <paramref name="names"/> picks, when its hash is not registered yet.</summary>
    private async ValueTask<Result<bool>> Ensure(RecipeId id, Func<ShippedFile, bool> names, CancellationToken cancellationToken)
    {
        if (shipped.Files().FirstOrDefault(file => file.Id == id && names(file)) is not { } file)
        {
            return false;   // not one this build ships: the planner refuses it as not found
        }

        var known = await store.Find(file.Hash, cancellationToken).ConfigureAwait(false);
        if (known.IsError || known.Value is not null)
        {
            return known.IsError ? known.Error : false;
        }

        var registered = await registrar.Register(file.Text, file.File, cancellationToken).ConfigureAwait(false);
        return registered.IsError ? registered.Error : true;
    }
}
