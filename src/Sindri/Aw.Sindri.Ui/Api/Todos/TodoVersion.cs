using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>The body of done, reopen, remove and restore (todo spec §4.4): the version the row showed; left out, the write applies to whatever is current.</summary>
/// <param name="Version">The todo's version, or null.</param>
internal sealed record TodoVersion([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Version = null);
