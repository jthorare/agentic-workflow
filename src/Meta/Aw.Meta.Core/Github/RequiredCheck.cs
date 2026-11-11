namespace Aw.Meta.Core.Github;

/// <summary>One required check of a pull request, as `gh pr checks --required` reports it.</summary>
/// <param name="Name">The check's name.</param>
/// <param name="State">GitHub's state (SUCCESS, FAILURE, IN_PROGRESS, ...); the bucket again when gh printed no state.</param>
/// <param name="Bucket">gh's bucket: pass, fail, pending, skipping or cancel.</param>
public sealed record RequiredCheck(string Name, string State, string Bucket);
