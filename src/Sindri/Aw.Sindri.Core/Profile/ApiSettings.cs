using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The local API's settings.</summary>
/// <param name="Port">The loopback port; 0 picks a free one.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApiSettings(int Port = ApiSettings.DefaultPort)
{
    /// <summary>The default port (factory addendum §17.29.1).</summary>
    public const int DefaultPort = 7410;
}
