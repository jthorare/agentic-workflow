using System.Text.Json.Serialization;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>The Messages tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(MessageTs))]
[JsonSerializable(typeof(MessageTsHolder))]
internal sealed partial class MessagesJsonContext : JsonSerializerContext;
