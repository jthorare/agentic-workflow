namespace Aw.Sindri.Core.Onyx;

/// <summary>The Onyx that `up` left running.</summary>
/// <param name="Port">The loopback port Onyx answers on.</param>
/// <param name="Url">http://127.0.0.1:&lt;port&gt;.</param>
/// <param name="Version">The release it reported.</param>
public sealed record OnyxStackInfo(int Port, string Url, string Version);
