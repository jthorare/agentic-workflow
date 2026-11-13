using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>The package's source-generated JSON context for the profile section it reads (used only through AwJson) and the key and choice lists errors carry as extension members.</summary>
[JsonSerializable(typeof(ContextSection))]
[JsonSerializable(typeof(ChecksSection))]
[JsonSerializable(typeof(CodeSection))]
[JsonSerializable(typeof(HistorySection))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<AnchorChoice>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
