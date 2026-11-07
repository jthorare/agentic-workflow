using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Lays a skills-folder trial's blocks over serve's catalog at run time, each checked against the pin its approval took (skill flows spec §7).</summary>
/// <param name="skills">The skill blocks; each reads a folder for one trial.</param>
/// <param name="approvals">The approvals, whose pins the folder's blocks are held to.</param>
/// <param name="contents">Reads the stored folder fact.</param>
/// <param name="json">The package's AwJson options (they hash the descriptors).</param>
internal sealed class TrialBlocks(IEnumerable<ISkillBlocks> skills, IRecipeApprovals approvals, IFactContents contents, JsonSerializerOptions json)
{
    /// <summary><paramref name="catalog"/> with the folder's blocks that <paramref name="approved"/> names laid over it.</summary>
    /// <param name="approved">The trial's approved recipe.</param>
    /// <param name="catalog">Serve's catalog.</param>
    /// <param name="folder">The stored '--skills' folder fact.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The catalog; an error when the folder can't be read or a block no longer matches its approved pin.</returns>
    internal async ValueTask<Result<BlockCatalog>> Overlay(ApprovedRecipe approved, BlockCatalog catalog, FactValue folder, CancellationToken cancellationToken)
    {
        var read = await contents.Read(folder, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var path = SkillLaunches.Unwrapped(System.Text.Encoding.UTF8.GetString(read.Value.Span));
        var named = PinRules.NamedBlocks(approved.Recipe).ToHashSet();
        var fromFolder = skills.SelectMany(b => b.LoadFolder(path).Loaded).Select(static b => b.Descriptor).Where(d => named.Contains(d.Id)).ToList();
        var approval = await approvals.Latest(approved.Version.Id, approved.Version.Version, approved.Version.Hash, cancellationToken).ConfigureAwait(false);
        if (approval.IsError)
        {
            return approval.Error;
        }

        // Every block the recipe names is held to its pin as the run will see it: a pinned block the folder no longer has, or no
        // longer parses, falls back to serve's, which fails here unless it is the very block the trial was approved on.
        var overlaid = catalog.Overlaid(fromFolder);
        var pins = approval.Value?.Pins.Blocks ?? [];
        foreach (var id in named)
        {
            var pin = pins.FirstOrDefault(p => p.Block == id);
            var held = pin is null
                ? fromFolder.TrueForAll(d => d.Id != id)
                : overlaid.Find(id) is { } descriptor && pin.Hash == PinRules.Hash(descriptor, json);
            if (!held)
            {
                return SindriErrors.RequestInvalid.With($"{id.Value} is not the block this trial was approved on: the skills folder changed since this trial started (fix: start the trial again)");
            }
        }

        return overlaid;
    }
}
