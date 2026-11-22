using System.Text.Json.Serialization;
using Aw.Transcripts.Core.Refs;

namespace Aw.Transcripts.Tests.Refs;

[JsonSerializable(typeof(SessionRef))]
[JsonSerializable(typeof(TurnRef))]
internal sealed partial class RefsJsonContext : JsonSerializerContext;
