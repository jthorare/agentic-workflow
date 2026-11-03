using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Judge;

/// <summary>The places a 'step.judge.choose' image may come from: the item's checkout and the ui-evidence output under $AW_STATE_DIR (images spec 3.4); nothing else is ever sent to a third party.</summary>
public interface IStepImageRoots
{
    /// <summary>The absolute directories an image must sit under (symlinks followed).</summary>
    /// <param name="repo">The instance's repo, or null when it has none (then only the ui-evidence output counts).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The directories; the checkout is left out when the repo's checkout is unknown.</returns>
    public ValueTask<IReadOnlyList<string>> Of(RepoName? repo, CancellationToken cancellationToken);
}
