using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The plan of an approval by version or by hash, with a shipped version that waits for its own approval registered first (#983): <see cref="RecipeApprovalPlanner"/>'s lookups behind
/// <see cref="ShippedRecipeRegistration"/>. Without a registration (a host that ships no recipes) it is the planner alone.
/// </summary>
/// <param name="planner">Plans the approval.</param>
/// <param name="registration">Registers a shipped version that is not registered yet; null plans only what is registered.</param>
/// <param name="files">The embedded set, to find a newer shipped child; null leaves the child notes out.</param>
/// <param name="store">The registered recipes, for the version of a pinned child.</param>
public sealed class ShippedApprovalPlans(RecipeApprovalPlanner planner, ShippedRecipeRegistration? registration = null, ShippedRecipeSet? files = null, IRecipeStore? store = null)
{
    /// <summary>The plan for 'approve &lt;id&gt; &lt;version&gt;'.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">Its version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The plan, or the registration's or the planner's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, int version, CancellationToken cancellationToken)
    {
        var registered = registration is null ? Results.Success(value: false) : await registration.EnsureVersion(id, version, cancellationToken).ConfigureAwait(false);
        return registered.IsError ? registered.Error : await Annotated(await planner.Approve(id, version, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The plan for 'approve &lt;id&gt;@&lt;hash&gt;', the hash whole or by a prefix of 8 or more digits.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash or its prefix.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The plan, or the registration's or the planner's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, string hash, CancellationToken cancellationToken)
    {
        var registered = registration is null ? Results.Success(value: false) : await registration.EnsureHash(id, hash, cancellationToken).ConfigureAwait(false);
        return registered.IsError ? registered.Error : await Annotated(await planner.Approve(id, hash, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The plan with a note on each child it pins: the version, and a warning when this build ships a newer one that waits for its approval (#1042).</summary>
    private async ValueTask<Result<ApprovalPlan>> Annotated(Result<ApprovalPlan> planned, CancellationToken cancellationToken)
    {
        if (planned.IsError || files is null || store is null || planned.Value.Preview.Pins.Children.Count == 0)
        {
            return planned;
        }

        var shipped = files.Files();
        List<PreviewChildNote> notes = [];
        foreach (var pin in planned.Value.Preview.Pins.Children)
        {
            var note = await NoteFor(store, pin, shipped, cancellationToken).ConfigureAwait(false);
            if (note.IsError)
            {
                return note.Error;
            }

            notes.Add(note.Value);
        }

        return planned.Value with { Preview = planned.Value.Preview with { ChildNotes = notes } };
    }

    private static async ValueTask<Result<PreviewChildNote>> NoteFor(IRecipeStore store, ChildPin pin, IReadOnlyList<ShippedFile> shipped, CancellationToken cancellationToken)
    {
        var versions = await store.Versions(pin.Recipe, cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        var pinned = versions.Value.FirstOrDefault(r => r.Hash == pin.Hash);
        var newer = shipped.FirstOrDefault(f => f.Id == pin.Recipe && f.Hash != pin.Hash && (pinned is null || f.Version > pinned.Version));
        return new PreviewChildNote(pin.Recipe, pin.Hash, pinned?.Version, newer?.Hash, newer?.Version, NoteText(pin, pinned, newer));
    }

    private static string NoteText(ChildPin pin, RegisteredRecipe? pinned, ShippedFile? newer)
    {
        var pinnedWords = $"{pin.Recipe.Value}@{pin.Hash.Display}" + (pinned is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" (version {pinned.Version})"));
        return newer is null
            ? $"pins {pinnedWords}"
            : string.Create(CultureInfo.InvariantCulture, $"pins {pinnedWords}; a newer shipped version waits for its approval: {pin.Recipe.Value}@{newer.Hash.Display} (version {newer.Version})");
    }
}
