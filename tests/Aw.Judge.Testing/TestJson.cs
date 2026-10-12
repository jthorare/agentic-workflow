using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Judge.Testing;

/// <summary>The AwJson options over the test question's context.</summary>
public static class TestJson
{
    /// <summary>The options.</summary>
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(TestingJsonContext.Default);
}
