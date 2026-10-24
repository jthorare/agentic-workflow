using System.Text.Json.Serialization;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// The providers' wire formats (the agent CLIs' output envelopes, the image input they read, Jev's request and
/// response). They are internal to Infrastructure, so the Host's JudgeJsonContext can't name them; this context is
/// only ever used through <see cref="WireJson.Options"/> (the AwJson profile).
/// </summary>
[JsonSerializable(typeof(CliEnvelope))]
[JsonSerializable(typeof(CodexEvent))]
[JsonSerializable(typeof(ImageEvidence))]
[JsonSerializable(typeof(JevRequest))]
[JsonSerializable(typeof(JevResponse))]
[JsonSerializable(typeof(JevBatchRequest))]
[JsonSerializable(typeof(JevBatchResponse))]
internal sealed partial class ProviderWireJsonContext : JsonSerializerContext;
