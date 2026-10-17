namespace Aw.Sindri.Core.Onyx;

/// <summary>One service of the pinned compose file, as Sindri reads it.</summary>
/// <param name="Name">The service name, such as api_server.</param>
/// <param name="Image">The service's `image:` exactly as written (still holding ${…} references), or null when it has none.</param>
/// <param name="Profiles">The service's `profiles:` names (empty for a service that always starts): compose starts the service only when one of them is enabled.</param>
public sealed record ComposeService(string Name, string? Image, IReadOnlyList<string> Profiles);
