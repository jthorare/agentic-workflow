namespace Aw.Sindri.Core.Resources;

/// <summary>A named folder with a size limit.</summary>
/// <param name="Name">Lowercase letters, digits and '-'.</param>
/// <param name="Size">The limit as the profile writes sizes: '512MiB', '4GiB'.</param>
public sealed record DiskResource(string Name, string Size);
