namespace Aw.Context.Testing.Fixtures;

/// <summary>The synthetic author and committer of every commit in the fixture repo.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Email">An address under the reserved '.invalid' domain.</param>
public sealed record FixtureAuthor(string Name, string Email);
