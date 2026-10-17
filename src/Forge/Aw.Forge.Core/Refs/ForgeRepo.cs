namespace Aw.Forge.Core.Refs;

/// <summary>A repository on a forge host.</summary>
/// <param name="Host">The host, lower case.</param>
/// <param name="Owner">The owner or organisation.</param>
/// <param name="Name">The repository name.</param>
public sealed record ForgeRepo(string Host, string Owner, string Name)
{
    /// <summary>A stable key, host/owner/name in lower case.</summary>
    public string Key => $"{Host}/{Owner}/{Name}".ToLowerInvariant();
}
