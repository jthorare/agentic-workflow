using System.Text.Json.Serialization;

namespace Aw.Transcripts.Infrastructure.Wire;

/// <summary>
/// The agents' transcript line formats. They are internal to Infrastructure, so a host's context can't name them; this
/// context is only used through <see cref="WireJson.Options"/> (the AwJson profile).
/// </summary>
[JsonSerializable(typeof(ClaudeLine))]
[JsonSerializable(typeof(CodexLine))]
[JsonSerializable(typeof(CursorLine))]
internal sealed partial class TranscriptWireJsonContext : JsonSerializerContext;
