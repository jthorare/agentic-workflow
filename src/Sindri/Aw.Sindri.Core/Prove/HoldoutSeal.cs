namespace Aw.Sindri.Core.Prove;

/// <summary>A phase's <c language="text">seal.json</c> (line spec §9.1): the set's hash and the plan packet's, written when the set was sealed.</summary>
/// <param name="SetHash">The set file's hash.</param>
/// <param name="PlanHash">The plan packet's hash the set was written for.</param>
public sealed record HoldoutSeal(string SetHash, string PlanHash);
