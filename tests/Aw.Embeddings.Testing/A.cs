namespace Aw.Embeddings.Testing;

/// <summary>The builder root: A.&lt;Thing&gt;() with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>The pinned model name the builders use.</summary>
    public const string Model = "nomic-embed-text:latest";

    /// <summary>The pinned digest the builders use.</summary>
    public const string Digest = "sha256:0a109f422b47e3a30ba2b10eca18548e944e8a23073ee3f3e947efcf3c45e59f";

    /// <summary>Gets a fake Ollama with the pinned model installed at the pinned digest.</summary>
    /// <returns>The fake.</returns>
    public static FakeOllamaHandler Ollama()
    {
        var fake = new FakeOllamaHandler();
        fake.Models[Model] = Digest;
        return fake;
    }

    /// <summary>Gets options for the pinned model on the loopback address.</summary>
    /// <param name="url">The server URL.</param>
    /// <returns>The options.</returns>
    public static OllamaOptions Options(string url = "http://127.0.0.1:11434") => new(url, Model, Digest);
}
