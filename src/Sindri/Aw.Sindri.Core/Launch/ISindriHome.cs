namespace Aw.Sindri.Core.Launch;

/// <summary>Where Sindri keeps its state on this box (AW_SINDRI_HOME or the default): 'session wrap' reads the per-launch files under it.</summary>
public interface ISindriHome
{
    /// <summary>Sindri's home directory.</summary>
    public string Path { get; }
}
