namespace Aw.Sindri.Core.Onyx;

/// <summary>Where the Onyx pins come from: the lock file the build embeds.</summary>
public interface IOnyxPinsSource
{
    /// <summary>Gets the pins this build was made with.</summary>
    public OnyxPins Pins { get; }
}
