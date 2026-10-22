namespace Aw.Sindri.Core.Setup;

/// <summary>Where doctor learns whether the embedded recipe set is HEAD's: a read of the build stamp and of git, starting nothing.</summary>
public interface IRecipeBuild
{
    /// <summary>The two tree hashes.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The trees, or null when this build has no stamp (it wasn't published by sindri-app).</returns>
    public ValueTask<RecipeTrees?> Read(CancellationToken cancellationToken);
}
