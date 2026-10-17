namespace Aw.Sindri.Core.Onyx;

/// <summary>One pinned image of the Onyx release for one platform (a row `image` of context/onyx.lock).</summary>
/// <param name="Repository">The image repository, such as docker.io/onyxdotapp/onyx-backend.</param>
/// <param name="Tag">The image tag the digests were measured from, such as v4.9.0.</param>
/// <param name="Platform">linux/arm64 or linux/amd64.</param>
/// <param name="Digest">The platform manifest's digest, sha256: and 64 lowercase hex digits.</param>
public sealed record OnyxImagePin(string Repository, string Tag, string Platform, string Digest);
