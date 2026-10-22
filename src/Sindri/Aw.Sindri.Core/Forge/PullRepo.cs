namespace Aw.Sindri.Core.Forge;

/// <summary>A repository on a forge host.</summary>
/// <param name="Host">The host, such as github.com. It must be on the profile's 'workflow.forge.hosts'.</param>
/// <param name="Owner">The owner or organisation.</param>
/// <param name="Name">The repository's name.</param>
public sealed record PullRepo(string Host, string Owner, string Name);
