namespace Aw.Sindri.Core.Profile;

/// <summary>Says whether the live profile file is on disk. The profile source answers "no profile" for a file it can't read as well as for one that isn't there; this tells the two apart (#3113).</summary>
public interface IProfileFile
{
    /// <summary>Whether the live profile file exists.</summary>
    /// <returns>True when the file is on disk, readable or not.</returns>
    public bool Exists();
}
