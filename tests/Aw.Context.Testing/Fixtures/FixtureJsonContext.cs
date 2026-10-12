using System.Text.Json.Serialization;

namespace Aw.Context.Testing.Fixtures;

/// <summary>The source-generated JSON context for the fixture's own files (history and probes), read through AwJson.</summary>
[JsonSerializable(typeof(FixtureHistory))]
[JsonSerializable(typeof(ProbeFile))]
internal sealed partial class FixtureJsonContext : JsonSerializerContext;
