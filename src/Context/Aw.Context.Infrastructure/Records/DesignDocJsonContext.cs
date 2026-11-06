using System.Text.Json.Serialization;
using Aw.Context.Core.Library;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The JSON text columns of 'design_doc' (migration 0031): a doc's repos, issues and errors. Internal to Infrastructure.</summary>
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(DesignDocError[]))]
internal sealed partial class DesignDocJsonContext : JsonSerializerContext;
