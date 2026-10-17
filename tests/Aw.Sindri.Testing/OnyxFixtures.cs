namespace Aw.Sindri.Testing;

/// <summary>The v4.9.0 deployment files committed under tests/Aw.Sindri.Tests/Fixtures/onyx-v4.9.0, copied beside the test assembly.</summary>
public static class OnyxFixtures
{
    /// <summary>Gets the fixtures directory next to the running test assembly.</summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "onyx-v4.9.0");

    /// <summary>Reads a fixture by its lock path, such as docker_compose/docker-compose.yml.</summary>
    /// <param name="lockPath">The path under deployment/.</param>
    /// <returns>The file's bytes.</returns>
    public static byte[] ReadBytes(string lockPath) => File.ReadAllBytes(Path.Combine(Root, lockPath));

    /// <summary>Reads a fixture as text.</summary>
    /// <param name="lockPath">The path under deployment/.</param>
    /// <returns>The file's text.</returns>
    public static string ReadText(string lockPath) => File.ReadAllText(Path.Combine(Root, lockPath));
}
