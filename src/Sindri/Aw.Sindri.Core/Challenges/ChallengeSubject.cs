using System.Globalization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Challenges;

/// <summary>
/// What a challenge is bound to (engine spec §4.2): the recipe hash and version, plus the pins digest on every
/// approval (the spec's §4.2 says drift approvals; review run 1 bound it on first approvals too); the instance and its version (a forwarded answer adds the child's); or the profile hash (S1-D5). If the
/// subject changes before the answer, the challenge is refused.
/// </summary>
/// <param name="Key">The subject, for example 'recipe:sha256:…@1'.</param>
/// <param name="Digest">The pins digest of a recipe approval (every approval binds it), or null for a revoke or the profile.</param>
public sealed record ChallengeSubject(string Key, string? Digest)
{
    /// <summary>A recipe approval or revoke.</summary>
    /// <param name="hash">The recipe hash.</param>
    /// <param name="version">Its version.</param>
    /// <param name="pinsDigest">The pins digest for an approval (always bound), or null for a revoke.</param>
    /// <returns>The subject.</returns>
    public static ChallengeSubject ForRecipe(RecipeHash hash, int version, string? pinsDigest) =>
        new(string.Create(CultureInfo.InvariantCulture, $"recipe:{hash.Value}@{version}"), pinsDigest);

    /// <summary>An answer to an instance's park or escalation.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="version">The instance version the preview showed.</param>
    /// <returns>The subject.</returns>
    public static ChallengeSubject ForInstance(WorkflowInstanceId instance, long version) =>
        new(string.Create(CultureInfo.InvariantCulture, $"instance:{instance.Value}@{version}"), Digest: null);

    /// <summary>A profile approval (S1-D5: replaces the 'confirmed' flag).</summary>
    /// <param name="hash">The profile hash.</param>
    /// <returns>The subject.</returns>
    public static ChallengeSubject ForProfile(ProfileHash hash) => new("profile:" + hash.Value, Digest: null);

    /// <summary>An approve-all: bound to the plan's digest, so a plan that changed since the preview is refused.</summary>
    /// <param name="digest">The plan's digest.</param>
    /// <returns>The subject.</returns>
    public static ChallengeSubject ForApproveAll(string digest) => new("approve-all", digest);
}
