using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>Writes the pointer to the Linear write key (`context.sources.linear.writeToken`) into the live profile, keeping every other key and comment as they were. The pointer is never the key itself.</summary>
public interface ILinearWriteTokenWriter
{
    /// <summary>Sets `context.sources.&lt;source&gt;.writeToken` to <paramref name="secretPointer"/>.</summary>
    /// <param name="source">The source key: `linear`.</param>
    /// <param name="secretPointer">The secret pointer, such as `keychain:sindri-index/linear-write`.</param>
    /// <returns>True when the file changed; false when the pointer was already as given; SND-INDEX-011 when the profile has no such entry; SND-PROFILE-002 when the layout is one Sindri won't edit.</returns>
    public Result<bool> SetWriteToken(string source, string secretPointer);

    /// <summary>Removes `context.sources.&lt;source&gt;.writeToken`.</summary>
    /// <param name="source">The source key: `linear`.</param>
    /// <returns>True when the file changed; false when there was no such key; SND-INDEX-011 when the profile has no such entry; SND-PROFILE-002 when the layout is one Sindri won't edit.</returns>
    public Result<bool> RemoveWriteToken(string source);
}
