using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.Testing;

/// <summary>An edit the fake was sent.</summary>
/// <param name="Key">The issue key.</param>
/// <param name="Body">The opened and set fields.</param>
internal sealed record LinearChangeSent(string Key, LinearChangeBody Body);
