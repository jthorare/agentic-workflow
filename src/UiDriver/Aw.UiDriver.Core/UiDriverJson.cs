using System.Text.Json;
using Aw.Kernel.Json;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Core;

#pragma warning disable MA0182 // used by the Avalonia adapter and the CLI, which the analyzer can't see from here

/// <summary>The package's JSON options: the Kernel profile over <see cref="UiDriverJsonContext"/>, shared by the server and the client.</summary>
internal static class UiDriverJson
{
    /// <summary>The options.</summary>
    internal static JsonSerializerOptions Options { get; } = Create();

    // A tree answer nests two JSON levels per element (the object and its Children array), so the default depth of 64 would cut it at about 31 levels.
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(AwJson.CreateOptions(UiDriverJsonContext.Default)) { MaxDepth = (2 * TreeRequest.MaxDepth) + 8 };
        options.MakeReadOnly();
        return options;
    }
}
