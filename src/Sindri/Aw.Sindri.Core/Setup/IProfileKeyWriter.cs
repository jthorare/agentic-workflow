using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>Edits one scalar key of the live profile file in place ('sindri profile set'): the text stays as it was except that key's line (or the lines for the parents it creates).</summary>
public interface IProfileKeyWriter
{
    /// <summary>Plans the edit of <paramref name="keyPath"/> to <paramref name="value"/> and writes nothing.</summary>
    /// <param name="keyPath">The dotted key, such as `context.models.labeling.pick`.</param>
    /// <param name="value">The value as typed; the writer decides whether it is a boolean, a number or a string.</param>
    /// <returns>The file, the edited text and whether it differs; SND-CLI-003 for a key that isn't a dotted path; SND-PROFILE-002 for a layout Sindri won't edit (a flow-style parent, a map or list at the key); SND-PROFILE-001 when the file can't be read.</returns>
    public Result<ProfileKeyEdit> Plan(string keyPath, string value);

    /// <summary>Writes a planned edit atomically (a temporary file renamed over the profile), keeping the file's mode. Nothing is written when the edit is unchanged.</summary>
    /// <param name="edit">The plan from <see cref="Plan"/>.</param>
    /// <returns>The profile file's path; SND-PROFILE-001 when it can't be written.</returns>
    public Result<string> Save(ProfileKeyEdit edit);
}
