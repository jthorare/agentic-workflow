namespace Aw.Sindri.Features.GetRepoStatus;

/// <summary>Where a directory's repo stands against the approved profile: a read that takes no lock, writes nothing and starts nothing.</summary>
/// <param name="Directory">The directory (the CLI's working directory, or the path it was given).</param>
public sealed record GetRepoStatusRequest(string Directory);
