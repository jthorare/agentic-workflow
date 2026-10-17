namespace Aw.Sindri.Core.Onyx;

/// <summary>A container image named by repository and tag, as the lock pins it.</summary>
/// <param name="Repository">The normalized repository: a registry is always present (docker.io when none was written) and a bare Docker Hub name gets library/, such as docker.io/library/redis.</param>
/// <param name="Tag">The tag, such as v4.9.0.</param>
public sealed record ImageRef(string Repository, string Tag);
