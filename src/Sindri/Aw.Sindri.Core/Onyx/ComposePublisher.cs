namespace Aw.Sindri.Core.Onyx;

/// <summary>One published port of a container.</summary>
/// <param name="Address">The host address it is bound to, such as 127.0.0.1 or 0.0.0.0.</param>
/// <param name="PublishedPort">The port on the host.</param>
/// <param name="TargetPort">The port in the container.</param>
public sealed record ComposePublisher(string Address, int PublishedPort, int TargetPort);
