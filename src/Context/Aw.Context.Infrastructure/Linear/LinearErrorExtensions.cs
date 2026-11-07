namespace Aw.Context.Infrastructure.Linear;

/// <summary>The extensions of a GraphQL error.</summary>
/// <param name="Code">The Code Linear serves.</param>
/// <param name="UserPresentableMessage">The UserPresentableMessage Linear serves: its own words for the person, when it has some.</param>
internal sealed record LinearErrorExtensions(string? Code = null, string? UserPresentableMessage = null);
