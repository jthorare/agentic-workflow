using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>The labels a labeling pass already got, by a key made of the cluster's scrubbed files (never its positional id), so a rebuild asks the model only about new clusters (q149).</summary>
public interface ILabelCache
{
    /// <summary>Finds the labels saved under these keys.</summary>
    /// <param name="keys">The keys to look up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The label of each key that is saved (a missing key is absent), or the error.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Find(IReadOnlyList<string> keys, CancellationToken cancellationToken);

    /// <summary>Saves labels, replacing a key that is already saved.</summary>
    /// <param name="labels">Each key with its label.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the error.</returns>
    public ValueTask<Result<Unit>> Save(IReadOnlyList<KeyValuePair<string, string>> labels, CancellationToken cancellationToken);
}
