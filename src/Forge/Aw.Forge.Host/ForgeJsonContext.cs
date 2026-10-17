using System.Text.Json.Serialization;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Forge.Host.Commands;
using Aw.Kernel;

namespace Aw.Forge.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions. Enums are written by name.</summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(ForgeRef))]
[JsonSerializable(typeof(PullSnapshot))]
[JsonSerializable(typeof(IReadOnlyList<ForgeThread>))]
[JsonSerializable(typeof(SeedResult))]
[JsonSerializable(typeof(Applied))]
internal sealed partial class ForgeJsonContext : JsonSerializerContext;
