using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>
/// Says whether a profile edit of <see cref="ISourceTokenProfileWriter"/> or <see cref="ILinearWriteTokenWriter"/> would apply, without writing anything. A caller that also touches the keychain asks first, so a refused edit
/// never leaves a stored secret with no pointer (or a deleted one with its pointer still in the profile).
/// </summary>
public interface ILinearProfileEditCheck
{
    /// <summary>Whether <see cref="ISourceTokenProfileWriter.SetToken"/> would apply.</summary>
    /// <param name="source">The source key: `linear`.</param>
    /// <param name="secretPointer">The pointer it would set.</param>
    /// <returns>Success when it would apply; the error the edit would return otherwise (SND-INDEX-011, SND-PROFILE-002).</returns>
    public Result<bool> CheckSetToken(string source, string secretPointer);

    /// <summary>Whether <see cref="ISourceTokenProfileWriter.SetTeams"/> would apply.</summary>
    /// <param name="source">The source key: `linear`.</param>
    /// <param name="teams">The teams it would set.</param>
    /// <returns>Success when it would apply; the error the edit would return otherwise.</returns>
    public Result<bool> CheckSetTeams(string source, IReadOnlyList<string> teams);

    /// <summary>Whether <see cref="ILinearWriteTokenWriter.SetWriteToken"/> would apply.</summary>
    /// <param name="source">The source key: `linear`.</param>
    /// <param name="secretPointer">The pointer it would set.</param>
    /// <returns>Success when it would apply; the error the edit would return otherwise.</returns>
    public Result<bool> CheckSetWriteToken(string source, string secretPointer);

    /// <summary>Whether <see cref="ILinearWriteTokenWriter.RemoveWriteToken"/> would apply.</summary>
    /// <param name="source">The source key: `linear`.</param>
    /// <returns>Success when it would apply; the error the edit would return otherwise.</returns>
    public Result<bool> CheckRemoveWriteToken(string source);
}
