namespace Aw.Context.Core.WholeMap;

/// <summary>What an answer says about the map it read.</summary>
/// <param name="MapVersion">The version ('v13').</param>
/// <param name="MapCommit">The commit it was built at (first seven characters), or null.</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="AsOf">When the version was written.</param>
/// <param name="Repo">The repo the answer is for, or null.</param>
public sealed record WholeMapBasis(string MapVersion, string? MapCommit, bool Draft, DateTimeOffset AsOf, string? Repo);
