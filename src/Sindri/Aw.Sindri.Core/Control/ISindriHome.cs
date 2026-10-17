namespace Aw.Sindri.Core.Control;

/// <summary>Where Sindri keeps its state: AW_SINDRI_HOME, default $AW_STATE_DIR/sindri-app.</summary>
public interface ISindriHome
{
    /// <summary>Gets the state directory, which may not exist yet on a fresh box.</summary>
    public string Path { get; }
}
