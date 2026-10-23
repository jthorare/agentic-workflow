using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The 'desk:' rows of the instances that wait on the owner at a human-verb wait, read at request time: the folded view has no recipe, so it cannot know a wait
/// state's exit is the owner's click. A wait state with a 'command.&lt;verb&gt;' exit whose effect acts with a human's authority is such a wait (no recipe is named).
/// </summary>
/// <param name="instances">Lists the open instances.</param>
/// <param name="directory">Resolves an instance's address.</param>
/// <param name="reader">The instances' views.</param>
/// <param name="registry">Reads an instance's pinned recipe back by its hash.</param>
/// <param name="catalog">Says which effects act with a human's authority.</param>
/// <param name="views">The 'items' view, for each item's title and repo.</param>
/// <param name="contents">Reads the pull request's status fact.</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="clock">The time the instance's own sentence is served at.</param>
internal sealed class NeedsYouOwnerStops
{
    // Red stub: the constructor only checks its ports; the implementation commit replaces it with a primary constructor.
    public NeedsYouOwnerStops(IWorkflowInstances instances, IInstanceDirectory directory, IInstanceReader reader, RecipeRegistryReader registry, BlockCatalog catalog, IViewReader views, IFactContents contents, ITextScrubber scrubber, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(scrubber);
        ArgumentNullException.ThrowIfNull(clock);
    }

    /// <summary>The owner-stop rows <paramref name="caller"/> may see.</summary>
    /// <param name="caller">The caller; a session sees only its repo's and the repo-less ones.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when the instances can't be listed, and a row whose read fails is left off.</returns>
    public ValueTask<IReadOnlyList<NeedsYouRow>> Read(CallerContext caller, CancellationToken cancellationToken) => throw new NotSupportedException();
}
