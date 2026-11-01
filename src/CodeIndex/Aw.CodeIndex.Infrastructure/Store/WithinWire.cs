using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>The AwJson options over <see cref="WithinJson"/>.</summary>
internal static class WithinWire
{
    /// <summary>The options.</summary>
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(WithinJson.Default);
}
