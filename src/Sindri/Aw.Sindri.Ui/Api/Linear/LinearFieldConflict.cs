using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>One field of a 409 edit: what the sheet opened with and what Linear says now.</summary>
/// <param name="Field">The field name, as in the change body (stateId, title, ...).</param>
/// <param name="Opened">The value the sheet opened with.</param>
/// <param name="Linear">The value Linear has now.</param>
internal sealed record LinearFieldConflict(string Field, JsonElement Opened, JsonElement Linear);
