using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>What the sandbox step reads besides the plan.</summary>
/// <param name="SystemdRun">The systemd-run program (AW_SANDBOX_SYSTEMD_RUN or <c lang="text">systemd-run</c>).</param>
/// <param name="Box">The box's RAM and cores.</param>
/// <param name="SindriHome">The real AW_SINDRI_HOME the session home sits beside.</param>
/// <param name="LaunchSecretsDirectory">The launcher's directory of per-launch secret files: a launch's file is moved from there into its session home.</param>
/// <param name="Control">The running control plane's endpoint record (control.json's content), or null when it is not known.</param>
internal sealed record SandboxLaunchOptions(string SystemdRun, BoxFacts Box, string SindriHome, string LaunchSecretsDirectory, Func<ControlEndpoint?> Control);
