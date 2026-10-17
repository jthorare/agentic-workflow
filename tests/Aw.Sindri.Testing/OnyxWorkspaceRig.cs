using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IOnyxWorkspace"/> under test with the paths it uses and a way to put raw text where its state lives.</summary>
public abstract class OnyxWorkspaceRig : IDisposable
{
    /// <summary>Gets the port under test.</summary>
    public abstract IOnyxWorkspace Workspace { get; }

    /// <summary>Gets the paths its files live at.</summary>
    public abstract OnyxPaths Paths { get; }

    /// <summary>Replaces the state file's content with raw text (to corrupt it).</summary>
    /// <param name="text">The text.</param>
    public abstract void WriteRawState(string text);

    /// <inheritdoc />
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
