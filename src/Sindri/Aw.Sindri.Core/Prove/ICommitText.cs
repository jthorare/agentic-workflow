using Aw.Kernel;

namespace Aw.Sindri.Core.Prove;

/// <summary>A phase's commits as text, for the canary scan (line spec §9.3). Git is the adapter's; the Step sees only the text.</summary>
public interface ICommitText
{
    /// <summary>The messages and patches of the commits after <paramref name="baseSha"/> up to <paramref name="head"/>, as one text.</summary>
    /// <param name="checkout">The repo's checkout.</param>
    /// <param name="baseSha">The phase's base commit (hex).</param>
    /// <param name="head">The built head (hex).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text, empty when the range holds no commit; an error when a commit is not a hex sha or the range can't be read.</returns>
    public ValueTask<Result<string>> Range(string checkout, string baseSha, string head, CancellationToken cancellationToken);
}
