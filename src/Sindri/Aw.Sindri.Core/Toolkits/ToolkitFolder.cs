namespace Aw.Sindri.Core.Toolkits;

/// <summary>A toolkit folder under the toolkits root.</summary>
/// <param name="Name">The toolkit name: the folder's name.</param>
/// <param name="Dir">The folder's absolute path.</param>
public sealed record ToolkitFolder(string Name, string Dir);
