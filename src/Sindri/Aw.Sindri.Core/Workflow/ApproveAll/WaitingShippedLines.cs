using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The shipped versions that wait for their own approval, in the recipe list (#983): a shipped version that gains a capability is left out of approve-all and is not registered until someone approves it,
/// so the registry's lines show only the old approved hash. Each such version gets its line here, with its hash and a mark that it gains a capability, read from the same plan the needs-you row is.
/// A version that is registered already is marked; one that is not is read from the embedded set the way a registration would store it.
/// </summary>
/// <param name="shipped">The shipped recipes and where they stand.</param>
/// <param name="files">The embedded set, for the waiting version's text.</param>
/// <param name="registry">Reads a recipe's line.</param>
/// <param name="json">The package's AwJson options (they write the normalized form).</param>
public sealed class WaitingShippedLines(IShippedRecipes shipped, ShippedRecipeSet files, RecipeRegistryReader registry, JsonSerializerOptions json)
{
    /// <summary>The registry's lines with the waiting shipped versions marked, and a line for each that is not registered.</summary>
    /// <param name="registered">The registry's lines.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The lines, the waiting ones last.</returns>
    public async ValueTask<Result<IReadOnlyList<RecipeSummary>>> Merge(IReadOnlyList<RecipeSummary> registered, CancellationToken cancellationToken)
    {
        var plan = await shipped.Plan(cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var lines = registered.ToList();
        var embedded = files.Files();
        foreach (var own in plan.Value.OwnApproval)
        {
            if (embedded.FirstOrDefault(f => f.Id == own.Id) is not { } file)
            {
                continue;
            }

            var gains = own.Reason == OwnApprovalReason.GainsCapability;
            var at = lines.FindIndex(l => l.Hash == file.Hash);
            if (at >= 0)
            {
                lines[at] = lines[at] with { GainsCapability = gains ? true : null };
                continue;
            }

            var line = await registry.Summary(Registered(file), cancellationToken).ConfigureAwait(false);
            if (line.IsError)
            {
                return line.Error;
            }

            lines.Add(line.Value with { GainsCapability = gains ? true : null });
        }

        return lines;
    }

    // The row a registration would keep: the normalized long form by hash. Nothing is written.
    private RegisteredRecipe Registered(ShippedFile file) =>
        new(file.Id, file.Version, file.Hash, Encoding.UTF8.GetString(AwJson.Serialize(file.Recipe with { Hash = null }, json)), file.Recipe.Base, DateTimeOffset.UnixEpoch);
}
