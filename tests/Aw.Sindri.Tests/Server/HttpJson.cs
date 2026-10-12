using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel.Json;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Server;

/// <summary>The package's AwJson options, for reading API answers in tests.</summary>
internal static class HttpJson
{
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(SindriJsonContext.Default);

    public static JsonTypeInfo<T> Info<T>() => (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
}
