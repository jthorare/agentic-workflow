namespace Aw.Sindri.Core.Onyx;

/// <summary>How Docker spells an image reference back: without its default registry and library namespace.</summary>
public static class ImageNames
{
    /// <summary>Drops a leading `docker.io/` and then `library/`, the way `docker image inspect` prints a Docker Hub reference.</summary>
    /// <param name="reference">A reference such as docker.io/library/nginx@sha256:….</param>
    /// <returns>For example nginx@sha256:…; a reference from another registry is unchanged.</returns>
    public static string WithoutDefaults(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var name = reference.StartsWith("docker.io/", StringComparison.Ordinal) ? reference["docker.io/".Length..] : reference;
        return name.StartsWith("library/", StringComparison.Ordinal) ? name["library/".Length..] : name;
    }
}
