namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What the API answers a command it took (the instance after it and the nonce are in the body too; the app reads the instance again instead).</summary>
/// <param name="AlreadyApplied">True when the same command had already been applied, so nothing new happened.</param>
internal sealed record ApplyCommandAccepted(bool AlreadyApplied);
