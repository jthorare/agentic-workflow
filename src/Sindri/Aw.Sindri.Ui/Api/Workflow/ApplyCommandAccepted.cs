namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What the API answers a command it took (the instance after it and the nonce are in the body too; the app reads the instance again instead).</summary>
/// <param name="AlreadyApplied">True when the same command had already been applied, so nothing new happened.</param>
/// <param name="Viewer">What the takeover's viewer did, or null (any other command, or a server that does not serve it).</param>
/// <param name="Attach">The attach command to run at a terminal, or null.</param>
internal sealed record ApplyCommandAccepted(bool AlreadyApplied, LaunchViewerView? Viewer = null, IReadOnlyList<string>? Attach = null);
