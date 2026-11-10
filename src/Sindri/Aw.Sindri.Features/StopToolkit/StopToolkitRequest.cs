namespace Aw.Sindri.Features.StopToolkit;

/// <summary>Stops a running or paused toolkit.</summary>
/// <param name="Name">The toolkit name.</param>
public sealed record StopToolkitRequest(string Name);
