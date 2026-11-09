namespace Aw.Sindri.Core.Resources;

/// <summary>The sandbox size an owner asks for. A key left out takes the profile's value.</summary>
/// <param name="Cpu">A percent such as '50%', or null.</param>
/// <param name="Memory">A size such as '4GiB', or null.</param>
/// <param name="Wall">A wall-clock limit such as '45m', or null.</param>
public sealed record SandboxResource(string? Cpu = null, string? Memory = null, string? Wall = null);
