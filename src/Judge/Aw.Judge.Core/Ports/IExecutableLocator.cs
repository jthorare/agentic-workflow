namespace Aw.Judge.Core.Ports;

/// <summary>Finds executables on the search path (which agent CLIs are installed).</summary>
public interface IExecutableLocator
{
    /// <summary>Whether an executable file of this name is in a directory on the search path.</summary>
    /// <param name="name">The binary name, e.g. 'codex'.</param>
    /// <returns>True when found.</returns>
    public bool IsOnPath(string name);
}
