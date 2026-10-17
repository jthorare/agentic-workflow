namespace Aw.Meta.Core.Ports;

/// <summary>The stamp the build put in the running binary.</summary>
public interface IBuildInfo
{
    /// <summary>Reads the binary's commit, build time, OS and runtime identifier.</summary>
    /// <returns>The stamp; fields the build did not stamp read "unknown".</returns>
    public BuildInfo Read();
}
