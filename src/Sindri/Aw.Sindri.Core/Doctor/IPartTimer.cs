namespace Aw.Sindri.Core.Doctor;

/// <summary>Takes how long one part of a doctor check ran (#3390), so the slow part can be named; an implementation logs it at debug level.</summary>
public interface IPartTimer
{
    /// <summary>Records one part's time.</summary>
    /// <param name="part">The part, such as <c lang="text">Launcher.terminal</c>.</param>
    /// <param name="elapsedMilliseconds">How long it ran.</param>
    public void Took(string part, long elapsedMilliseconds);
}
