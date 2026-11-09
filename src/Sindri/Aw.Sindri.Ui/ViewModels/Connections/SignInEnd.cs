using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>The end of a sign-in the app waited on.</summary>
/// <param name="State">How it ended.</param>
/// <param name="Connection">The connection as last read, or null.</param>
/// <param name="Detail">Why it failed, in words; empty when it did not.</param>
internal sealed record SignInEnd(SignInState State, ConnectionView? Connection, string Detail);
