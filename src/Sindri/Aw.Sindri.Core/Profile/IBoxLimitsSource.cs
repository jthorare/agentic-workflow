namespace Aw.Sindri.Core.Profile;

/// <summary>The box's limits as they are now: the heavy-slot cap can drop after a profile was approved, so a check that repeats the profile's cross-key rule reads it fresh.</summary>
public interface IBoxLimitsSource
{
    /// <summary>Reads the limits.</summary>
    /// <returns>The limits now.</returns>
    public BoxLimits Read();
}
