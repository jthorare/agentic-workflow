using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IOnyxHealth"/> under test and the port to probe.</summary>
public abstract class OnyxHealthRig : IDisposable
{
    /// <summary>Gets the port under test.</summary>
    public abstract IOnyxHealth Health { get; }

    /// <summary>Gets the port to probe.</summary>
    public abstract int Port { get; }

    /// <inheritdoc />
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
