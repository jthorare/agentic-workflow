namespace Aw.Context.Core.Linear;

/// <summary>
/// Where the Linear read key comes from when the host knows more than the profile's pointer: Sindri answers with the Connections page's Linear connection first and the pointer second (#2508, the order GitHub and Slack have). Context can't see Sindri's connections,
/// so this is the seam; without one the read key is the pointer alone.
/// </summary>
public interface ILinearReadCredential
{
    /// <summary>Finds the read key.</summary>
    /// <param name="profilePointer">'context.sources.linear.token', or null when the profile names none.</param>
    /// <returns>The key, or null when neither a connection nor the pointer gives one.</returns>
    public string? TokenFor(string? profilePointer);
}
